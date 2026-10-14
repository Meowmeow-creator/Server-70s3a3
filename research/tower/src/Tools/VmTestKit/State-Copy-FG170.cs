using System;using System.IO;using System.Reflection;using System.Collections;using System.Collections.Generic;
// Content-verified XML reuse only. No delayed writes or relaxed validation.
class Offline70StateCopyFG170 {
 class Cached {internal byte[] Bytes;internal object State;}
 static readonly Dictionary<string,Cached> Cache=new Dictionary<string,Cached>(StringComparer.OrdinalIgnoreCase);
 static readonly Dictionary<Type,FieldInfo[]> Fields=new Dictionary<Type,FieldInfo[]>();
 internal static object Copy(object value){
  if(value==null)return null;Type t=value.GetType();if(t.IsValueType||t==typeof(string))return value;
  if(t.IsArray){Array source=(Array)value;if(source.Rank!=1)throw new InvalidDataException("State array rank");Array result=Array.CreateInstance(t.GetElementType(),source.Length);for(int i=0;i<source.Length;i++)result.SetValue(Copy(source.GetValue(i)),i);return result;}
  IList list=value as IList;if(list!=null){IList result=(IList)Activator.CreateInstance(t);foreach(object item in list)result.Add(Copy(item));return result;}
  if(!t.Name.StartsWith("Offline70",StringComparison.Ordinal))throw new InvalidDataException("State copy type "+t.FullName);
  FieldInfo[] fs;if(!Fields.TryGetValue(t,out fs)){fs=t.GetFields(BindingFlags.Instance|BindingFlags.Public);if(t.GetProperties(BindingFlags.Instance|BindingFlags.Public).Length!=0)throw new InvalidDataException("State copy public property");Fields.Add(t,fs);}
  object clone=Activator.CreateInstance(t);foreach(FieldInfo f in fs)f.SetValue(clone,Copy(f.GetValue(value)));return clone;
 }
 static bool Canonical(string path){string n=Path.GetFileName(path);return n=="economy-account1-yoyo-FG.xml"||n=="progression-yoyo-FG.xml";}
 internal static bool Try(string path,byte[] bytes,Type type,out object state){state=null;if(!Canonical(path))return false;Cached c;if(!Cache.TryGetValue(Path.GetFullPath(path),out c)||c.State.GetType()!=type||c.Bytes.Length!=bytes.Length)return false;for(int i=0;i<bytes.Length;i++)if(c.Bytes[i]!=bytes[i])return false;state=Copy(c.State);return true;}
 internal static void Put(string path,byte[] bytes,object state){if(!Canonical(path))return;string full=Path.GetFullPath(path);if(!Cache.ContainsKey(full)&&Cache.Count>=4)Cache.Clear();Cache[full]=new Cached{Bytes=bytes,State=Copy(state)};}
}
