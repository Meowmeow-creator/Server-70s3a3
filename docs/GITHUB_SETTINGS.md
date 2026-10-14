# GitHub 协作设置

本次提供的仓库是公开仓库。源码内容可被任何人读取；若只与朋友合作，可由仓库管理员改为私有。不要把真实存档、凭据或原版资源上传后再依赖删文件补救。

在 Settings → Collaborators 添加朋友的GitHub账号，授予 Write；不必给 Admin。朋友账号尚未提供，本次未自动发邀请。

在 Settings → Rules → Rulesets 对 main 设置：要求 PR、至少一人批准、重新提交后旧批准失效、检查 `repository-checks` 与 `compile-stable`、禁止删除和强推，并限制跳过规则的人员。研究编译失败也要修复。CODEOWNERS 的 zzdiandian 用于标出核心审查范围，可在朋友加入后明确分工；文件本身不等于已经开启分支保护。

个人分支 `feature/...`、`fix/...`、`research/...`。正式发布前记下 commit、稳定数据哈希、构建环境、内部验证与真人验收范围，并明确未完成清单。上传源码基线不表示已经发布新的可运行游戏版本。
