using System;
using System.IO;
using System.Collections.Generic;

namespace MECore
{
    /* The namespace contains a set of classes and methods for work with .ini and .mini files (which are used in the Unity project
    for describing game objects). Working with content from .ini files makes it possible to save current info about Unity game objects' status.
    The content is developing at the moment and contains some features that are not debugged yet.*/ 
    public static class FuncLibrary
    {

        public static void VerifyType(Type T)
        {
            if ((T != typeof(bool)) && (T != typeof(string)) && (T != typeof(double)))
                throw new TypeAccessException("This type is not yet supported by properties");
        }
        public static bool IsComment(char c)
        {
            string set = "#;";
            if (set.Contains(c.ToString())) return true;
            return false;
        }
        public static bool ContainesComment(string s)
        {
            if (s.Contains(";") || s.Contains("#")) return true;
            return false;
        }
        public static string GetComment(string s)
        {
            int a = s.IndexOf('#');
            int b = s.IndexOf(';');
            if (a < 0)
            {
                if (b < 0) return null;
                return s.Substring(b);
            }
            else
            {
                if (b > 0) return s.Substring((a < b)? a : b);
                return s.Substring(a);
            }
        }
    }
    // The class guarantees existance of a file at the specified path
    public class Address
    {
        public Address(string src)
        {
            if (File.Exists(src))
            {
                this.src = src;
            }
            else
            {
                throw new FileNotFoundException($"Error! File {src} does not exist.");
            }
        }
        public readonly string src;
        public override string ToString()
        {
            return src; 
        }
    }
    public class IniProperty<T>
    {
        public IniProperty(string key, T value, string comment = null)
        {
            FuncLibrary.VerifyType(value.GetType());
            this.key = key; this.value = value;
            if (comment != null) this.comment = comment;
            if (this.comment != null) if ((this.comment[0] != ';') && (this.comment[0] != '#')) this.comment.Insert(0, "#");
        }
        public IniSection parent;
        public readonly string key;
        protected T value;
        public string comment;
        public bool inherited = false, modified = false, cloned = false;
        public T Value
        {
            get { return value; }
            set
            {
                modified = true;
                this.value = value;
            }
        }
        public IniProperty<T> Clone()
        {
            var ret = new IniProperty<T>(key, value)
            {
                cloned = true,
                parent = parent
            };
            return ret;
        }
        public override string ToString()
        {
            if (comment == null)
            return key + " = " + value.ToString();
            return key + " = " + value.ToString() + " " + comment;
        }
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            var mod = obj as IniProperty<T>;
            if (parent != mod.parent) return false;
            if (key == (mod.key)) return true;
            else return false;
        }
        public override int GetHashCode()
        {
            return key.GetHashCode() + value.GetHashCode() + parent.GetHashCode();
        }
        public static bool operator==(IniProperty<T> arg1, IniProperty<T> arg2)
        {
            return arg1.Equals(arg2);
        }
        public static bool operator !=(IniProperty<T> arg1, IniProperty<T> arg2)
        {
            return !(arg1.Equals(arg2));
        }
    }
    public class IniSection
    {
        public IniSection(string[] sarr, IniFile If = null)
        {
            BProps = new List<IniProperty<bool>>();
            DProps = new List<IniProperty<double>>();
            SProps = new List<IniProperty<string>>();
            Inhers = new List<IniSection>();
            // getting section header with inheritance list
            int i = 1;
            for (; i < sarr[0].Length; i++)
            {
                if (sarr[0][i] != ']') continue;
                name = sarr[0].Substring(1, i - 1);
                break;
            }
            if (sarr[0].Contains(":"))
            {
                if (If != null)
                {
                    parent = If;
                    if (!If.Initialised) If.Initialise();
                    List<string> queue = new List<string>();
                    string temp = null;
                    while (++i < sarr[0].Length)
                    {
                        if (!((sarr[0][i] == ':') || (sarr[0][i] == ' ')))
                        {
                            if (sarr[0][i] != ',')
                            {
                                temp += sarr[0][i];
                            }
                            else
                            {
                                queue.Add(temp);
                                temp = null;
                            }
                        }
                    }
                    if (!queue.Contains(temp)) queue.Add(temp);
                    foreach (var item in queue)
                    {
                        for (int j = 0; j < If.Lis.Count; j++)
                        {
                            if (If.Lis[j].name == item) Inherit(If.Lis[j], true);
                        }
                    }
                }
                else throw new ArgumentNullException("The section was declared as local (without parent file initialisation), but " +
                    "inherits from other sections defined in the file. Set reference to the file to initialise it for success.");
            }
            // getting properties
            for (i = 1; i < sarr.Length; i++)
            {
                // getting key
                string key = null;
                bool untilEqualChar = true;
                for (int j = 0; j < sarr[i].Length; j++)
                {
                    if (untilEqualChar)
                    {
                        if (sarr[i][j] == '=')
                        {
                            key = sarr[i].Substring(0, j - 1); key.Trim();
                            untilEqualChar = false;
                        }
                    }
                    else break;
                }
                // dividing value and comment
                string temp = sarr[i].Substring(sarr[i].IndexOf('=') + 1);
                string comm = null;
                if (FuncLibrary.ContainesComment(sarr[i]))
                {
                    comm = FuncLibrary.GetComment(sarr[i]);
                    temp = temp.Substring(0, temp.IndexOf(comm[0]));
                }
                temp.Trim();
                // checking for references
                temp = GetOwnValues(temp, ref comm);
                // calculating expressions
                if (temp.Contains("(") || (comm != null && (comm.Contains("{") || comm.Contains("exp"))))
                {
                    if (!comm.Contains("{")) comm = (comm == null) ? $"# {{ {temp} }}" : comm + $" {{ {temp} }}";
                    temp = Calculate(temp);
                }
                // constructing property
                bool isDouble = double.TryParse(temp, out double d);
                temp = temp.Trim();
                if (isDouble)
                {
                    CreateProperty(key, d, comm);
                    continue;
                }
                if ((temp == "+") || (temp == "true"))
                {
                    CreateProperty(key, true, comm);
                    continue;
                }
                if ((temp == "-") || (temp == "false"))
                {
                    CreateProperty(key, false, comm);
                    continue;
                }
                CreateProperty(key, temp, comm);
            }
        }
        public IniSection(string name, params IniSection[] inherits)
        {
            BProps = new List<IniProperty<bool>>();
            DProps = new List<IniProperty<double>>();
            SProps = new List<IniProperty<string>>();
            Inhers = new List<IniSection>();
            this.name = name;
            foreach (var item in inherits)
            {
                Inherit(item);
            }
        }
        public readonly string name;
        public readonly IniFile parent;
        public List<IniSection> Inhers { get; private set; }
        public List<IniProperty<bool>> BProps { get; private set; }
        public List<IniProperty<double>> DProps { get; private set; }
        public List<IniProperty<string>> SProps { get; private set; }
        public void Inherit(IniSection IS, bool allowReplace = true)
        {
            Inhers.Add(IS);
            var binhs = new IniProperty<bool>[IS.BProps.Count];
            var dinhs = new IniProperty<double>[IS.DProps.Count];
            var sinhs = new IniProperty<string>[IS.SProps.Count];
            int i = -1;
            foreach (var item in binhs)
            {
                binhs[++i] = IS.BProps[i].Clone();
                binhs[i].inherited = true;
                binhs[i].parent = this;
            }
            i = -1;
            foreach (var item in dinhs)
            {
                dinhs[++i] = IS.DProps[i].Clone();
                dinhs[i].inherited = true;
                dinhs[i].parent = this;
            }
            i = -1;
            foreach (var item in sinhs)
            {
                sinhs[++i] = IS.SProps[i].Clone();
                if (sinhs[i].comment != null)
                {
                    if (sinhs[i].comment.Contains("slf"))
                    {
                        sinhs[i].Value = sinhs[i].Value.Replace(IS.name, name);
                    }
                    else if (sinhs[i].comment.Contains("{"))
                    {
                        int l = sinhs[i].comment.IndexOf("{") + 1, r = sinhs[i].comment.IndexOf("}");
                        var x = sinhs[i].comment.Substring(l, r - l);
                        string Null = null; // useless, should be redone
                        x = GetOwnValues(x, ref Null);
                        sinhs[i].Value = Calculate(x);
                    }
                }
                sinhs[i].inherited = true;
                sinhs[i].parent = this;
            }
            BProps.AddRange(binhs);
            DProps.AddRange(dinhs);
            SProps.AddRange(sinhs);
            Validate(allowReplace);
        }
        public void CreateProperty(string k, object val, string comment = null) 
        {
            object newprop;
            if (val is string)
            {
                newprop = new IniProperty<string>(k, (string)val, comment);
                IniProperty<string> prop = newprop as IniProperty<string>;
                prop.parent = this;
                SProps.Add(prop);
            }
            else
            {
                if ((val is double) || (val is int) || (val is long))
                {
                    newprop = new IniProperty<double>(k, (double)val, comment);
                    IniProperty<double> prop = newprop as IniProperty<double>;
                    prop.parent = this;
                    DProps.Add(prop);
                }
                else
                {
                    if (val is bool)
                    {
                        newprop = new IniProperty<bool>(k, (bool)val, comment);
                        IniProperty<bool> prop = newprop as IniProperty<bool>;
                        prop.parent = this;
                        BProps.Add(prop);
                    }
                    else throw new ArgumentException("Not valid type");
                }
            }
            
        }
        public dynamic GetProperty(string key, Type T)
        {
            FuncLibrary.VerifyType(T);
            if (T == typeof(string))
            {
                foreach (var item in SProps)
                {
                    if (item.key == key) return item;
                }
            }
            else
            {
                if (T == typeof(bool))
                {
                    foreach (var item in BProps)
                    {
                        if (item.key == key) return item;
                    }
                }
                else if (T == typeof(double))
                {
                    foreach (var item in DProps)
                    {
                        if (item.key == key) return item;
                    }
                }
            }
            return null;
        }
        public dynamic GetProperty(string key)
        {
            foreach (var item in SProps)
            {
                if (item.key == key) return item;
            }
            foreach (var item in DProps)
            {
                if (item.key == key) return item;
            }
            foreach (var item in BProps)
            {
                if (item.key == key) return item;
            }
            return null;
        }
        public List<IniProperty<string>> GetPropertiesByValue(string value, int first, int number)
        {
            var lips = new List<IniProperty<string>>();
            int i = -1;
            while (++i < SProps.Count)
            {
                if (SProps[i].Value == value)
                {
                    if ((--first <= 0) && (lips.Count != number))
                    {
                        lips.Add(SProps[i]);
                    }
                }
            }
            return lips;
        }
        public List<IniProperty<bool>> GetPropertiesByValue(bool value, int first, int number)
        {
            var lips = new List<IniProperty<bool>>();
            int i = -1;
            while (++i < SProps.Count)
            {
                if (BProps[i].Value == value)
                {
                    if ((--first <= 0) && (lips.Count != number))
                    {
                        lips.Add(BProps[i]);
                    }
                }
            }
            return lips;
        }
        public List<IniProperty<double>> GetPropertiesByValue(double value, int first, int number)
        {
            var lips = new List<IniProperty<double>>();
            int i = -1;
            while (++i < SProps.Count)
            {
                if (DProps[i].Value == value)
                {
                    if ((--first <= 0) && (lips.Count != number))
                    {
                        lips.Add(DProps[i]);
                    }
                }
            }
            return lips;
        }
        public void Validate(bool AllowOverwrite)
        {
            List<string> lis = new List<string>();
            if (BProps.Count > 0) for (int i = BProps.Count - 1; i >= 0; i--)
            {
                if (lis.Contains(BProps[i].key))
                {
                    if (AllowOverwrite)
                    {
                        BProps.RemoveAt(i);
                    }
                    else throw new FormatException("The section contains more than 1 similar keys. If you want to replace an old one" +
                        "set 'AllowOverwrite' to 'true'");
                }
                lis.Add(BProps[i].key);
            }
            if (DProps.Count > 0) for (int i = DProps.Count - 1; i >= 0; i--)
            {
                if (lis.Contains(DProps[i].key))
                {
                    if (AllowOverwrite)
                    {
                        DProps.RemoveAt(i);
                    }
                    else throw new FormatException("The section contains more than 1 similar keys. If you want to replace an old one" +
                        "set 'AllowOverwrite' to 'true'");
                }
                lis.Add(DProps[i].key);
            }
            if (SProps.Count > 0) for (int i = SProps.Count - 1; i >= 0; i--)
            {
                if (lis.Contains(SProps[i].key))
                {
                    if (AllowOverwrite)
                    {
                        SProps.RemoveAt(i);
                    }
                    else throw new FormatException("The section contains more than 1 similar keys. If you want to replace an old one" +
                        "set 'AllowOverwrite' to 'true'");
                }
                lis.Add(SProps[i].key);
            }
        }
        string GetOwnValues(string temp, ref string comm)
        {
            while (temp.IndexOf("$") > -1)
            {
                int a = temp.IndexOf("$");
                int b = temp.IndexOfAny(new char[] { ' ', '+', '-' }, a);
                if (b < 0) b = temp.Length;
                string c = temp.Substring(a + 1, b - a - 1);
                if (c == "this") // a special case, may be an expression, only string
                {
                    if (temp.Contains("(") || (comm != null && (comm.Contains("{") || comm.Contains("exp"))))
                    {
                        comm = (comm == null) ? $"# {{{temp}}}" : comm + $" {{{temp}}}";
                    }
                    else comm = (comm == null) ? "#slf" : comm + " slf";
                    temp = temp.Replace("$this", name);
                    break;
                }
                else
                {
                    comm = (comm == null) ? $"# {{{temp}}}" : comm + $" {{{temp}}}";
                    if (temp.Contains(".")) // accessing another section
                    {
                        var left = c.Substring(0, c.IndexOf("."));
                        var right = c.Substring(c.IndexOf(".") + 1);
                        var sct = parent.GetSectionByName(left);
                        object prp = sct.GetProperty(right);
                        if (typeof(IniProperty<bool>) == prp.GetType())
                        {
                            temp.Replace("$" + c, ((IniProperty<bool>)prp).Value.ToString().TrimStart());
                        }
                        else if (typeof(IniProperty<string>) == prp.GetType())
                        {
                            temp = temp.Replace("$" + c, ((IniProperty<string>)prp).Value.TrimStart());
                        }
                        else if (typeof(IniProperty<double>) == prp.GetType())
                        {
                            temp = temp.Replace("$" + c, ((IniProperty<double>)prp).Value.ToString().TrimStart());
                        }
                    }
                    else // accesing a property that has been already defined
                    {
                        object prp = GetProperty(c);
                        if (typeof(IniProperty<bool>) == prp.GetType())
                        {
                            temp = temp.Replace("$" + c, ((IniProperty<bool>)prp).Value.ToString().TrimStart());
                        }
                        else if (typeof(IniProperty<string>) == prp.GetType())
                        {
                            temp = temp.Replace("$" + c, ((IniProperty<string>)prp).Value.TrimStart());
                        }
                        else if (typeof(IniProperty<double>) == prp.GetType())
                        {
                            temp = temp.Replace("$" + c, ((IniProperty<double>)prp).Value.ToString().TrimStart());
                        }
                    }
                }
            }
            return temp;
        }
        string Calculate(string exp) // open brackets and calculate
        {
            while (exp.Contains("(")) // actually not working
            {
                int l = exp.IndexOf("(");
                int count = 1, i;
                for (i = l; (i < exp.Length) && (count > 0); i++)
                {
                    if (exp[i] == '(') count++;
                    if (exp[i] == ')') count--;
                }
                exp = exp.Replace(exp.Substring(l, i - l + 1), Calculate(exp.Substring(l + 1, i - l - 1)));
            }
            int ptr;
            while ((ptr = GetFirstOperator(exp)) >= 0)
            {
                if (((ptr + 1) < exp.Length) && (exp[ptr] == (exp[ptr + 1]))) // unary
                {
                    int j = 1;
                    for (int i = ptr + 1; i < exp.Length; i++)
                    {
                        if (exp[i] == exp[ptr]) j++;
                        else break;
                    }
                    bool plus = true;
                    if (exp[ptr] == '-') plus = false;
                    else if (exp[ptr] != '+') throw new FormatException("Just '+' and '-' unary operations are defined");
                    if (((ptr + j) >= exp.Length) || (exp[ptr + j] == ' ')) // postfix
                    {
                        if (plus)
                        {
                            char c = exp[ptr - 1];
                            exp = exp.Replace('+', c);
                        }
                        else
                        {
                            exp = exp.Substring(0, ptr - j);
                        }
                    }
                    else // prefix
                    {
                        if (plus)
                        {
                            char c = exp[ptr + j];
                            exp = exp.Replace('+', c);
                        }
                        else
                        {
                            exp = exp.Substring(ptr + 2 * j);
                        }
                    }
                }
                else // binary
                {
                    int i, j;
                    for (i = ptr + 2; i < exp.Length; i++)
                    {
                        if (exp[i] == ' ') break;
                    }
                    for (j = ptr - 2; j >= 0; j--)
                    {
                        if (exp[j] == ' ') break;
                    }
                    if (i == exp.Length) i--; if (j < 0) j = 0; // to avoid undesireable circumstances
                    string strOpr1 = exp.Substring((exp[j] == ' ') ? (j + 1) : j, ptr - 1);
                    string strOpr2 = exp.Substring(ptr + 2, (exp[i] == ' ') ? (i - ptr - 2) : (i - ptr - 1));
                    if (char.IsDigit(exp[ptr + 2]) && char.IsDigit(exp[ptr - 2])) // arithmetics
                    {
                        double opr1 = double.Parse(strOpr1);
                        double opr2 = double.Parse(strOpr2);
                        double res = double.NaN;
                        switch (exp[ptr])
                        {
                            case '+':
                                res = opr1 + opr2;
                                break;
                            case '-':
                                res = opr1 - opr2;
                                break;
                            case '*':
                                res = opr1 * opr2;
                                break;
                            case '/':
                                res = opr1 / opr2;
                                break;
                            case '^':
                                res = Math.Pow(opr1, opr2);
                                break;
                        }
                        if (exp[i] == ' ') i--; if (exp[j] == ' ') j++; // more comfortable
                        exp = exp.Replace(exp.Substring(j, i - j + 1), res.ToString());
                    }
                    else // just string
                    {
                        string res;
                        switch (exp[ptr])
                        {
                            case '+':
                                res = strOpr1 + strOpr2;
                                break;
                            case '-':
                                res = strOpr1.Replace(strOpr2, null);
                                break;
                            default:
                                throw new FormatException("Just '+' and '-' are allowed with string operators");
                        }
                        if (exp[i] == ' ') i--; if (exp[j] == ' ') j++; // more comfortable
                        exp = exp.Replace(exp.Substring(j, i - j + 1), res);
                    }
                }
            }
            return exp;
        }
        int GetFirstOperator(string exp)
        {
            int[] ops = new int[5];
            string str = "+-*/^";
            for (int i = 0; i < 5; i++)
            {
                ops[i] = exp.IndexOf(str[i]);
            }
            int x = ops[0];
            foreach (var item in ops)
            {
                if ((item < x) && (item >= 0)) x = item;
            }
            return x;
        }
        public string[] ToString(bool simple = true)
        {
            if (simple)
            {
                int n = BProps.Count + DProps.Count + SProps.Count + 1;
                var sarr = new string[n];
                n = 0;
                sarr[n] = $"[{name}]";
                foreach (var item in BProps) sarr[++n] = item.ToString();
                foreach (var item in DProps) sarr[++n] = item.ToString();
                foreach (var item in SProps) sarr[++n] = item.ToString();
                return sarr;
            }
            else
            {
                int n = 1;
                foreach (var item in BProps)
                {
                    if (item.inherited)
                    {
                        if (item.modified) n++;
                    }
                    else n++;
                }
                foreach (var item in DProps)
                {
                    if (item.inherited)
                    {
                        if (item.modified) n++;
                    }
                    else n++;
                }
                foreach (var item in SProps)
                {
                    if (item.inherited)
                    {
                        if (item.modified) n++;
                    }
                    else n++;
                }
                var sarr = new string[n];
                sarr[n = 0] = $"[{name}] ";
                if (Inhers.Count > 0)
                {
                    sarr[n] += ": ";
                    for (int i = 0; i < Inhers.Count; i++)
                    {
                        sarr[n] += (Inhers[i].name) + (((i + 1) == Inhers.Count) ? "" : ", ");
                    }
                }
                foreach (var item in BProps)
                {
                    if (!((item.inherited) && !(item.modified)))
                    {
                        sarr[++n] = item.ToString();
                    }
                }
                foreach (var item in DProps)
                {
                    if (!((item.inherited) && !(item.modified)))
                    {
                        sarr[++n] = item.ToString();
                    }
                }
                foreach (var item in SProps)
                {
                    if (!((item.inherited) && !(item.modified)))
                    {
                        sarr[++n] = item.ToString();
                    }
                }
                return sarr;
            }
        }
    }
    public class IniFile
    {
        public IniFile(string loc, bool init = false)
        {
            Lis = new List<IniSection>();
            src = new Address(loc);
            Initialised = false;
            if (init) Initialise();
        }
        public bool Initialised { get; private set; }
        public readonly Address src;
        public List<IniSection> Lis { get; private set; }
        public static void Validate(IniFile IF)
        {
            var lis = new List<string>();
            foreach (var item in IF.Lis)
            {
                item.Validate(false);
                if (lis.Contains(item.name))
                    throw new FormatException("It is forbidden for .ini file to contain 2 or more similar section names.");
                lis.Add(item.name);
            }
        }
        public void Initialise() // 1. Sections without inheritance. 2. Sections with inheritance from sections created by step 1. 3-n. etc...
        {
            string loc = src.src;
            List<string> list = new List<string>();
            using (StreamReader sr = new StreamReader(new FileStream(loc, FileMode.Open, FileAccess.Read)))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    list.Add(line);
                }
            }
            List<int> il = new List<int>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Contains("[")) il.Add(i);
            }
            List<string> queue = new List<string>();
            Initialised = true;
            for (int i = 0; i < il.Count; i++)
            {
                if (!(list[il[i]].Contains(":")))
                {
                    if ((i + 1) < il.Count) Lis.Add(new IniSection(list.GetRange(il[i], il[i + 1] - il[i]).ToArray(), this));
                    else Lis.Add(new IniSection(list.GetRange(il[i], list.Count-il[i]).ToArray()));
                }
                else queue.Add(list[il[i]]);
            }
            List<string> inherit;
            string temp = null;
            int k = 0;
            while (queue.Count > 0)
            {
                int z = -1;
                while (++z < queue.Count)
                {
                    var item = queue[z];
                    inherit = new List<string>();
                    int i = item.IndexOf(':');
                    temp = null;
                    while (++i < item.Length)
                    {
                        if (!((item[i] == ':') || (item[i] == ' ')))
                        {
                            if (item[i] != ',')
                            {
                                temp += item[i];
                            }
                            else
                            {
                                inherit.Add(temp);
                                temp = null;
                            }
                        }
                    }
                    if (!inherit.Contains(temp)) inherit.Add(temp);
                    bool b = true;
                    foreach (var smth in inherit)
                    {
                        if (!SectionExists(smth)) b = false;
                    }
                    if (b)
                    {
                        int a = list.IndexOf(item);
                        int x = a;
                        while (++x < list.Count)
                        {
                            if (list[x].Contains("[")) break;
                        }
                        int c = x - a;
                        Lis.Add(new IniSection(list.GetRange(a, c).ToArray(), this));
                        int y = -1;
                        while (++y < queue.Count)
                        {
                            if (queue[y].Contains(item)) queue.Remove(queue[y]);
                        }
                    }
                }
                if (++k > 20) throw new FormatException("Inheritance is impossible. Maybe it is recursive. Check if the file is correct.");
            }
        }
        public bool SectionExists(string name)
        {
            foreach (var item in Lis)
            {
                if (item.name == name) return true;
            }
            return false;
        }
        public IniSection GetSectionByName(string name)
        {
            foreach (var item in Lis)
            {
                if (item.name == name) return item;
            }
            return null;
        }
        public static IniFile CreateIni(string src, bool AllowOverwrite)
        {
            if (File.Exists(src))
            {
                if (AllowOverwrite)
                {
                    File.Delete(src);
                }
                else throw new AccessViolationException("The file at the location already exists!");
            }
            File.Create(src);
            return new IniFile(src);
        }
        public void Save()
        {
            using (StreamWriter sw = new StreamWriter(new FileStream(src.src, FileMode.Open, FileAccess.Write)))
            {
                int size = Lis.Count;
                string[][] res = new string[size][];
                for (int i = 0; i < res.Length; i++)
                {
                    res[i] = Lis[i].ToString(false);
                    foreach (var item in res[i])
                    {
                        sw.WriteLine(item);
                    }
                }
            }
        }
        public void SaveAs(string newpath, FileAccess fa = FileAccess.Write, FileMode fm = FileMode.CreateNew)
        {
            using (StreamWriter sw = new StreamWriter(new FileStream(newpath, fm, fa)))
            {
                int size = Lis.Count;
                string[][] res = new string[size][];
                for (int i = 0; i < res.Length; i++)
                {
                    res[i] = Lis[i].ToString(false);
                    foreach (var item in res[i])
                    {
                        sw.WriteLine(item);
                    }
                }
            }
        }
    }
}
