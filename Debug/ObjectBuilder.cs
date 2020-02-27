using System;
using System.Reflection;
using System.Collections.Generic;
namespace MECore
{
    public class ObjectBuilder
    {
        public static object CreateInstance(Type type, string section, string path)
        {
            var props = type.GetProperties();
            var fields = type.GetFields();
            var x = Activator.CreateInstance(type);
            IniFile If = new IniFile(path, true);
            var sect = If.GetSectionByName(section);
            foreach (var item in fields)
            {
                var key = item.Name;
                item.SetValue(x, sect.GetProperty(key));
            }
            foreach (var item in props)
            {
                var key = item.Name;
                item.SetValue(x, sect.GetProperty(key));
            }
            return x;
        }
        public static object[] CreateArray(Type type, string path)
        {
            var props = type.GetProperties();
            var fields = type.GetFields();
            IniFile If = new IniFile(path, true);
            var rets = new object[If.Lis.Count];
            int i = -1;
            foreach (var item in If.Lis)
            {
                var x = Activator.CreateInstance(type);
                foreach (var thing in fields)
                {
                    var key = thing.Name;
                    thing.SetValue(x, item.GetProperty(key));
                }
                rets[++i] = x;
            }
            return rets;
        }
    }
}