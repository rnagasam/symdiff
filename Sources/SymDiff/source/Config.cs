using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Boogie;
using SymDiffUtils;


//note:
// file format:

// globals: (file1.id1, file2.id2); (file1.id3, file2.id4); ...
// types: (same as above)
// constants: (same as above)
// procedure: (file1.procname1, file2.procname2) / (arg1, arg2); (arg3, arg4); ...
// function: (same as above)

namespace SDiff
{
  public static class BadGlobalState
  {
    public static int MaxUFArgs = 0;
    public static int SumUFArgs = 0;
    public static int NumUFs = 0;

    public static int MaxCex = 0;
    public static int SumCex = 0;

    public static int NumProcDiffs = 0;
    public static int SumProcDiffs = 0;

    public static long MaxTime = 0;
    public static long SumTime = 0;
  }


  public class ConfigParserException : Exception
  {
    public ConfigParserException(string a) : base(a) { }
  }

  public class ProcedureMap : List<Duple<HDuple<string>, ParamMap>>
  {
    public readonly string prefix;

    public Dictionary <string, string> equivalentProcs;

    public ProcedureMap(string prefix)
      : base()
    {
      this.prefix = prefix;
      this.equivalentProcs = new Dictionary<string, string>();
    }

    public bool AddFromArray(string[] fs, string[] ps)
    {
      HDuple<string> d = HDuple<string>.OfArray(fs);
      ParamMap p = ParamMap.OfArray(ps);

      if (d == null)
        return true;

      if (p == null)
        p = new ParamMap();

      Add(new Duple<HDuple<string>, ParamMap>(d, p));
      return false;
    }

    public List<HDuple<string>> ToProcedureAssocList()
    {
      return this.Map(x => x.fst);
    }

    public Dictionary<string, string> ToProcedureDictionary()
    {
      var dict = new Dictionary<string, string>();
      foreach (var d in this)
        dict.Add(d.fst.fst, d.fst.snd);
      return dict;
    }

    public Dictionary<string, string> ToProcedureDictionaryWithoutPrefix(string keyPrefix, string valuePrefix)
    {
        var dict = new Dictionary<string, string>();
        foreach (var d in this)
        {
            var key = d.fst.fst.StartsWith(keyPrefix) ? d.fst.fst.Substring(keyPrefix.Length) : d.fst.fst;
            var value = d.fst.snd.StartsWith(valuePrefix) ? d.fst.snd.Substring(valuePrefix.Length) : d.fst.snd;
            dict.Add(key, value);
        }
        return dict;
    }

    public ParamMap FindPair(string s1, string s2)
    {
      foreach (var p in this)
        if (p != null && p.fst.fst == s1 && p.fst.snd == s2)
          return p.snd;
      return null;
    }

    public bool PairEquivalent (string s1, string s2)
    {
      return equivalentProcs.ContainsKey(s1) && equivalentProcs[s1] == s2 ||
      (equivalentProcs.ContainsKey(s2) && equivalentProcs[s2] == s1);
    }
    
    public void StoreEquivalent (string s1, string s2)
    {
      equivalentProcs[s1] = s2;
    }

    public override string ToString()
    {
      string r = "";
      foreach (var proc in this)
        if (proc != null)
          r += prefix + ": " + proc.fst + " / " + proc.snd + "\n";
      return r;
    }
  }

  public class SpecMap : ProcedureMap
  {
    /// Mutual summaries provided by users in config files are stored as strings.
    /// The <c>MutualSummary</c> class processes these and constructs the actual
    /// Boogie functions.
    private Dictionary <HDuple<string>, string> msTextMap;

    public SpecMap() : this("spec") {}

    public SpecMap(string prefix) : base(prefix)
    {
      msTextMap = new Dictionary<HDuple<string>, string>();
    }

    public void AddMutualSummary(string p1, string p2, string ms)
    {
      msTextMap.Add(new HDuple<string>(p1,p2), ms);
    }

    public bool ExistsMutualSummary(string p1, string p2)
    {
      return msTextMap.ContainsKey(new HDuple<string>(p1, p2));
    }

    public string FindMutualSummary(string p1, string p2)
    {
      if (msTextMap.TryGetValue(new HDuple<string>(p1, p2), out var ms))
        return ms;
      return null;
    }

    public override string ToString()
    {
      string r = "";
      foreach (var proc in this)
      {
        if (proc != null)
        {
          var (p1, p2) = proc.fst;
          var b = ExistsMutualSummary(p1, p2);
          r += (b ? "spec" : "procedure") + ": " + proc.fst + " / " + proc.snd + "\n";
          r += b ? "{\n" + FindMutualSummary(p1, p2) + "\n}" : "";
        }
      }
      return r;
    }
  }

  public class ParamMap : List<HDuple<string>>
  {
    public ParamMap() {}

    public ParamMap(List<HDuple<string>> p)
      : base(p)
    {
    }

    public ParamMap(HDuple<string>[] p)
      : this(new List<HDuple<string>>(p))
    {
    }

    public static ParamMap OfArray(string[] ps)
    {
      ParamMap m = new ParamMap();
      try
      {
        foreach (string p in ps)
        {
          var pr = HDuple<string>.OfArray(p.Split(','));
          if (pr != null)
            m.Add(pr);
        }
      }
      catch
      {
        return null;
      }
      return (ParamMap)m.Maybe();
    }

    public string MapsTo(string s)
    {
      //rewrite with find
      foreach (HDuple<string> d in this)
      {
        if (d == null)
          Log.Out(Log.Error, "Null in parameter map! Dumping:" + this);

        if (s == d.fst)
          return d.snd;
      }
      return null;
    }

    public ParamMap Filter(Predicate<HDuple<string>> f)
    {
      var npm = new ParamMap();
      foreach (var m in this)
        if (f(m))
          npm.Add(m);
      return npm;
    }

    public override string ToString()
    {
      string r = "";
      foreach (var ss in this)
        if (ss != null)
          r += ss + ";";
      return r;
    }

    public Dictionary<string, string> GetParamDictionary()
    {
        var dict = new Dictionary<string, string>();
        foreach (var d in this)
            dict.Add(d.fst, d.snd);
        return dict;

    }
  }

  public class Config
  {
    public SpecMap ProcedureMap { get; }

    public ProcedureMap FunctionMap { get; }

    private ParamMap globalMap;
    public ParamMap GlobalMap => new(globalMap);

    private ParamMap typeMap;
    public ParamMap TypeMap => new(typeMap);

    private ParamMap constMap;
    public ParamMap ConstMap => new(constMap);

    public Config(string filename) : this()
    {
      StreamReader in_s = new StreamReader(filename);

      string l;
      while (!in_s.EndOfStream)
      {
        l = in_s.ReadLine().Trim();
        if (l.StartsWith("#")) //comment
          continue;
        if (l.StartsWith("globals:"))
          ParseGlobals(l.Substring(8));
        else if (l.StartsWith("types:"))
          ParseTypes(l.Substring(6));
        else if (l.StartsWith("constants:"))
          ParseConstants(l.Substring(11));
        else if (l.StartsWith("function:"))
          ParseFunction(l.Substring(10));
        else if (l.StartsWith("procedure:"))
          ParseProcedure(l.Substring(11));
        else if (l.StartsWith("spec:"))
          ParseSpec(l.Substring(5), in_s);
        else
          continue;
      }

      return;
    }

    public Config()
    {
      ProcedureMap = new SpecMap("procedure");
      FunctionMap = new ProcedureMap("function");
      globalMap = new ParamMap();
      typeMap = new ParamMap();
      constMap = new ParamMap();
    }

    public void AddProcedure(Duple<HDuple<string>, ParamMap> mapping)
    {
      var p = ProcedureMap.FirstOrDefault(p => p.fst.fst.Equals(mapping.fst.fst));
      if (p == null) {
        ProcedureMap.Add(mapping);
      } else if (mapping.ToString() != p.ToString()) {
        Console.WriteLine ("mapping exists but doesn't match, " + mapping.ToString() + " != " + p.ToString());
        throw new ArgumentException($"{mapping.fst.fst} already exists in config.");
      }
    }

    public void AddProcedureSpec(Duple<HDuple<string>, ParamMap> mapping, string ms)
    {
      AddProcedure(mapping);
      this.ProcedureMap.AddMutualSummary(mapping.fst.fst, mapping.fst.snd, ms);
    }

    public void AddFunction(Duple<HDuple<string>, ParamMap> mapping)
    {
      FunctionMap.Add(mapping);
    }

    public void AddGlobal(HDuple<string> mapping)
    {
      globalMap.Add(mapping);
    }


    // TODO: Have this integrate with the config provided in the config file
    // Could split on base types, which are compared directly and others,
    // that need to come from the config.
    public bool TypeEq (Microsoft.Boogie.Type a, Microsoft.Boogie.Type b) {
      if (a != null && b != null) {
          return a.ToString() == b.ToString();
      } else {
          return a == b;
      }
    }

    public void AddType(HDuple<string> mapping)
    {
      typeMap.Add(mapping);
    }

    public void AddConstant(HDuple<string> mapping)
    {
      constMap.Add(mapping);
    }

    private bool ParseGlobals(string l)
    {
      globalMap.AddRange(ParseParamMap(l));
      return false;
    }

    private bool ParseTypes(string l)
    {
      typeMap.AddRange(ParseParamMap(l));
      return false;
    }

    private bool ParseConstants(string l)
    {
      constMap.AddRange(ParseParamMap(l));
      return false;
    }

    private ParamMap ParseParamMap(string l)
    {
      l = l.Replace("(", "").Replace(")", "").Replace(" ", "");
      string[] fs = l.Split(';');

      return new ParamMap(fs.Map(x => x.Split(',')).Map(x => HDuple<string>.OfArray(x))).Filter(x => x != null);
    }

    private HDuple<string> ParseIntoProcedureMap(string l, ProcedureMap map)
    {
      //(n,n)/(n',n');...
      l = l.Replace("(","").Replace(")","").Replace(" ","");
      string[] fs = l.Split('/');

      if (fs == null) return null;
      string[] ps;
      if (fs.Length == 1)
        ps = new string[0];
      else
        ps = fs[1].Split(';');

      string[] rp = fs[0].Split(',');
      map.AddFromArray(rp, ps);

      return HDuple<string>.OfArray(rp);
    }

    private bool ParseProcedure(string l)
    {
      ParseIntoProcedureMap(l, ProcedureMap);
      return false;
    }

    // Format in config file:
    // spec: (f1, f2) / params;
    // {
    //   mutual summary for (f1, f2)
    // }
    public void ParseSpec(string l, StreamReader instream)
    {
      // First line contains procedure and parameter names
      var (f1, f2) = ParseIntoProcedureMap(l, ProcedureMap);

      string StripBraces (string l)
      {
        int lskip = l.StartsWith('{') ? 1 : 0;
        int rskip = lskip + (l.EndsWith('}') ? 1 : 0);
        int sublen = l.Length - rskip;
        return l.Substring(lskip, sublen) + Environment.NewLine;
      }

      var buf = "";
      do
      {
        if (instream.EndOfStream)
          throw new ConfigParserException("Expected mutual summary after 'spec:' but found EOF instead");

        l = instream.ReadLine().Trim();

        if (buf == "" && l.Length > 0 && !l.StartsWith('{'))
          throw new ConfigParserException("Expected '{' on a newline after 'spec:' but found '" + l[0] + "' instead");

        buf += l.Length == 0 ? "" : StripBraces(l);
      } while (!l.EndsWith('}'));

      ProcedureMap.AddMutualSummary(f1, f2, buf.Trim());
    }

    private bool ParseFunction(string l)
    {
      ParseIntoProcedureMap(l, FunctionMap);
      return false;
    }

    public ParamMap FindProcedure(string f1, string f2)
    {
      return ProcedureMap.FindPair(f1, f2);
    }

    public string FindProvidedMutualSummary(string f1, string f2)
    {
      return ProcedureMap.FindMutualSummary(f1, f2);
    }

    public ParamMap FindFunction(string f1, string f2)
    {
      return FunctionMap.FindPair(f1, f2);
    }

    public List<HDuple<string>> GetProcedureAssocList()
    {
      return ProcedureMap.ToProcedureAssocList();
    }

    public Dictionary<string, string> GetProcedureDictionary()
    {
      return ProcedureMap.ToProcedureDictionary();
    }

    public string LookupFunction(string f)
    {
      foreach (var d in ProcedureMap)
        if (d.fst.fst == f)
          return d.fst.snd;
      return null;
    }

    public override string ToString()
    {
      var s = "";
      if (globalMap.Count != 0)
        s += "#globals\nglobals: " + globalMap + "\n";
      if (typeMap.Count != 0)
        s += "#types\ntypes: " + typeMap + "\n";
      if (constMap.Count != 0)
        s += "#constants\nconstants: " + constMap + "\n";
      if (FunctionMap.Count != 0)
        s += "#functions\n" + FunctionMap + "\n";
      if (ProcedureMap.Count != 0)
        s += "#procedures\n" + ProcedureMap + "\n";
      return s;
    }
  }
}
