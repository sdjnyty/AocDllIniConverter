using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.IO;
using Microsoft.Win32;
using Vestris.ResourceLib;
using IniParser;

namespace YTY.AocDllIniConverter
{
  public static class Commands
  {
    public static ICommand DllToIni { get; } = new DllToIniCommand();

    public static ICommand IniToDll { get; } = new IniToDllCommand();

    private class DllToIniCommand : ICommand
    {
      public event EventHandler CanExecuteChanged;

      public bool CanExecute(object parameter)
      {
        return true;
      }

      public void Execute(object parameter)
      {
        var ofn = new OpenFileDialog
        {
          Multiselect = true,
          InitialDirectory = Environment.CurrentDirectory,
          Filter = "dll|*.dll",
        };
        if (ofn.ShowDialog() == true)
        {
          var outputPath = $"{DateTime.Now:yyyyMMdd-hhmmss}.ini";
          var count = ExtractDllToIni(ofn.FileNames, Path.Combine(Environment.CurrentDirectory, outputPath));
          MessageBox.Show($"已导出到 {outputPath}\n共导出 {count} 条字串");
        }
      }

      private static int ExtractDllToIni(string[] dllFileNames, string iniFileName)
      {
        int count = 0;

        var ini = new IniParser.Model.IniData();
        ini.Configuration.AssigmentSpacer = string.Empty;

        foreach (var dll in dllFileNames)
        {
          var ri = new ResourceInfo();
          ri.Load(dll);

          var languages = ri[Kernel32.ResourceTypes.RT_STRING]
            .GroupBy(r=>r.Language);
          foreach (var language in languages)
          {
            var fileName = Path.GetFileNameWithoutExtension(dll);
            var culture = System.Globalization.CultureInfo.GetCultureInfo(language.Key).Name;
            var extension = Path.GetExtension(dll);
            var section = ini[$"{fileName}_{culture}{extension}"];
            foreach (StringResource resource in language)
            {
              foreach (var str in resource.Strings)
              {
                section[str.Key.ToString()] = str.Value.Replace("\n", @"\n");
                count++;
              }
            }
          }
        }

        new FileIniDataParser().WriteFile(iniFileName, ini, Encoding.UTF8);
        return count;
      }
    }

    private class IniToDllCommand : ICommand
    {
      public event EventHandler CanExecuteChanged;

      public bool CanExecute(object parameter)
      {
        return true;
      }

      public void Execute(object parameter)
      {
        var ofn = new OpenFileDialog
        {
          InitialDirectory = Environment.CurrentDirectory,
          Filter = "ini|*.ini",
        };
        if (ofn.ShowDialog() == true)
        {
          var outputPath = $"{DateTime.Now:yyyyMMdd-hhmmss}.ini";
          var result = ParseIniToDll(ofn.FileName);
          MessageBox.Show(string.Join("\n", result.Select(tup => $"已导出 {tup.Item2} 条字串到 {tup.Item1}")));
        }
      }

      private static IReadOnlyList<Tuple<string, int>> ParseIniToDll(string iniFileName)
      {
        var ret = new List<Tuple<string, int>>();

        var parser = new FileIniDataParser();
        parser.Parser.Configuration.AllowDuplicateKeys = true;
        parser.Parser.Configuration.OverrideDuplicateKeys = true;

        var ini = parser.ReadFile(iniFileName, Encoding.UTF8);
        var dirName = Path.GetFileNameWithoutExtension(iniFileName);
        Directory.CreateDirectory(dirName);
        foreach (var section in ini.Sections)
        {
          var outputFile = Path.Combine(dirName, section.SectionName);
          File.Copy("empty.dll", outputFile, true);

          var toWrite = new Dictionary<ushort, string>();
          foreach (var kv in section.Keys)
          {
            if (ushort.TryParse(kv.KeyName, out var key))
            {
              toWrite.Add(key, kv.Value.Replace(@"\n", "\n"));
            }
          }
          var groups = toWrite.OrderBy(kv => kv.Key).GroupBy(kv => kv.Key / 16 + 1);

          using (var ri = new ResourceInfo())
          {
            ri.Load(outputFile);
            var resources = new List<Resource>();
            foreach (var id in groups)
            {
              var sr = new StringResource((ushort)id.Key);
              foreach (var kv in id)
              {
                sr[kv.Key] = kv.Value;
              }
              resources.Add(sr);
            }
            Resource.Save(outputFile, resources);
          }
          ret.Add(Tuple.Create(outputFile, toWrite.Count));
        }
        return ret;
      }
    }
  }
}
