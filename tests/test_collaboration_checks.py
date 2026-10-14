import unittest, tempfile, shutil, subprocess, sys, json
from pathlib import Path

SCRIPT=Path(__file__).resolve().parents[1]/'scripts/check_repo.py'

class CollaborationChecks(unittest.TestCase):
    def check(self, xml='<Quests><Quest><Id>1</Id></Quest></Quests>', extras=None):
        with tempfile.TemporaryDirectory(prefix='dnf70-repo-check-') as folder:
            root=Path(folder)
            (root/'scripts').mkdir(); shutil.copy2(SCRIPT,root/'scripts/check_repo.py')
            (root/'baseline').mkdir(); (root/'baseline/manifest.json').write_text(json.dumps({'files':{'data/catalog.xml':'fixture'}}))
            (root/'data').mkdir(); (root/'data/catalog.xml').write_text(xml)
            for name,content in (extras or {}).items():
                p=root/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(content)
            return subprocess.run([sys.executable,str(root/'scripts/check_repo.py')],capture_output=True).returncode

    def test_same_item_in_different_shop_allowed(self):
        self.assertEqual(0,self.check('<Offers><Offer><Shop>2</Shop><ItemId>1106</ItemId></Offer><Offer><Shop>8</Shop><ItemId>1106</ItemId></Offer></Offers>'))
    def test_duplicate_quest_rejected(self):
        self.assertNotEqual(0,self.check('<Quests><Quest><Id>1</Id></Quest><Quest><Id>1</Id></Quest></Quests>'))
    def test_duplicate_shop_offer_rejected(self):
        self.assertNotEqual(0,self.check('<Offers><Offer><Shop>2</Shop><ItemId>1106</ItemId></Offer><Offer><Shop>2</Shop><ItemId>1106</ItemId></Offer></Offers>'))
    def test_tower_promotion_rejected(self):
        self.assertNotEqual(0,self.check(extras={'src/Tools/VmTestKit/Tower-Service-FG172.cs':'// fixture'}))
    def test_player_file_rejected(self):
        self.assertNotEqual(0,self.check(extras={'stores/role.xml':'<Role/>'}))
    def test_secret_rejected(self):
        self.assertNotEqual(0,self.check(extras={'docs/example.md':'gh'+'p_'+'A'*36}))
    def test_conflict_marker_rejected(self):
        self.assertNotEqual(0,self.check(extras={'src/Example.cs':'<'*7+' HEAD\n'}))
    def test_dtd_rejected(self):
        self.assertNotEqual(0,self.check('<!DOCTYPE Quests><Quests/>'))

if __name__=='__main__': unittest.main()
