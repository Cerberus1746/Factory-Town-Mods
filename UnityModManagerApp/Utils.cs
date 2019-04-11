using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using dnlib.DotNet;

namespace UnityModManagerNet.Installer
{
    static class Utils
    {
        static Utils()
        {

        }

        public static Version ParseVersion(string str)
        {
            string[] array = str.Split('.');
            if (array.Length >= 3)
            {
                Regex regex = new Regex(@"\D");
                return new Version(int.Parse(regex.Replace(array[0], "")), int.Parse(regex.Replace(array[1], "")), int.Parse(regex.Replace(array[2], "")));
            }
            else if (array.Length >= 2)
            {
                Regex regex = new Regex(@"\D");
                return new Version(int.Parse(regex.Replace(array[0], "")), int.Parse(regex.Replace(array[1], "")));
            }
            else if (array.Length >= 1)
            {
                Regex regex = new Regex(@"\D");
                return new Version(int.Parse(regex.Replace(array[0], "")), 0);
            }

            Log.Print($"Error parsing version '{str}'.");
            return new Version();
        }

        public static bool IsDirectoryWritable(string dirpath)
        {
            try
            {
                if (Directory.Exists(dirpath)) {
                    using (FileStream fs = File.Create(Path.Combine(dirpath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
                    {; }
                }

                return true;
            }
            catch
            {
                Log.Print($"Directory '{dirpath}' does not have write permission.");
                return false;
            }
        }

        public static bool IsFileWritable(string filepath)
        {
            try
            {
                if (File.Exists(filepath)) {
                    using (FileStream fs = File.OpenWrite(filepath))
                    {; }
                }

                return true;
            }
            catch
            {
                Log.Print($"File '{filepath}' does not have write permission.");
                return false;
            }
        }

        public static bool TryParseEntryPoint(string str, out string assembly)
        {
            assembly = string.Empty;
            return TryParseEntryPoint(str, out assembly, out _, out _, out _);
        }

        public static bool TryParseEntryPoint(string str, out string assembly, out string @class, out string method, out string insertionPlace) {
            assembly = string.Empty;
            @class = string.Empty;
            method = string.Empty;
            insertionPlace = string.Empty;

            MatchCollection matches;

            Regex regex = new Regex(@"(?:(?<=\[)(?'assembly'.+(?>\.dll))(?=\]))|(?:(?'class'[\w|\.]+)(?=\.))|(?:(?<=\.)(?'func'\w+))|(?:(?<=\:)(?'mod'\w+))", RegexOptions.IgnoreCase);
            string[] groupNames = regex.GetGroupNames();
            if(str == null) {
                matches = regex.Matches("");
            } else {
                matches = regex.Matches(str);
            }
           
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    foreach (string group in groupNames)
                    {
                        if (match.Groups[group].Success)
                        {
                            switch (group)
                            {
                                case "assembly":
                                    assembly = match.Groups[group].Value;
                                    break;
                                case "class":
                                    @class = match.Groups[group].Value;
                                    break;
                                case "func":
                                    method = match.Groups[group].Value;
                                    if (method == "ctor") {
                                        method = ".ctor";
                                    } else if (method == "cctor") {
                                        method = ".cctor";
                                    }

                                    break;
                                case "mod":
                                    insertionPlace = match.Groups[group].Value.ToLower();
                                    break;
                            }
                        }
                    }
                }
            }

            bool hasError = false;

            if (string.IsNullOrEmpty(assembly))
            {
                hasError = true;
                Log.Print("Assembly name not found.");
            }

            if (string.IsNullOrEmpty(@class))
            {
                hasError = true;
                Log.Print("Class name not found.");
            }

            if (string.IsNullOrEmpty(method))
            {
                hasError = true;
                Log.Print("Method name not found.");
            }

            if (hasError)
            {
                Log.Print($"Error parsing EntryPoint '{str}'.");
                return false;
            }

            return true;
        }

        public static bool TryGetEntryPoint(ModuleDefMD assemblyDef, string str, out MethodDef foundMethod, out string insertionPlace, bool createConstructor = false)
        {
            foundMethod = null;

            if (!TryParseEntryPoint(str, out string assembly, out string className, out string methodName, out insertionPlace))
            {
                return false;
            }

            TypeDef targetClass = assemblyDef.Types.FirstOrDefault(x => x.FullName == className);
            if (targetClass == null)
            {
                Log.Print($"Class '{className}' not found.");
                return false;
            }

            foundMethod = targetClass.Methods.FirstOrDefault(x => x.Name == methodName);
            if (foundMethod == null)
            {
                if (createConstructor && methodName == ".cctor")
                {
                    TypeDef typeDef = ModuleDefMD.Load(typeof(Utils).Module).Types.FirstOrDefault(x => x.FullName == typeof(Utils).FullName);
                    MethodDef method = typeDef.Methods.FirstOrDefault(x => x.Name == ".cctor");
                    if (method != null)
                    {
                        typeDef.Methods.Remove(method);
                        targetClass.Methods.Add(method);
                        foundMethod = method;
                        
                        return true;
                    }
                }
                Log.Print($"Method '{methodName}' not found.");
                return false;
            }

            return true;
        }

        public static string ResolveOSXFileUrl(string url)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = "osascript";
            p.StartInfo.Arguments = $"-e \"get posix path of posix file \\\"{url}\\\"\"";
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return output.TrimEnd();
        }

        public static bool IsUnixPlatform()
        {
            int p = (int)Environment.OSVersion.Platform;
            return (p == 4) || (p == 6) || (p == 128);
        }

        public static bool MakeBackup(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Copy(path, $"{path}.backup_", true);
                }
            }
            catch (Exception e)
            {
                Log.Print(e.Message);
                return false;
            }

            return true;
        }

        public static bool MakeBackup(string[] arr)
        {
            try
            {
                foreach (string path in arr)
                {
                    if (File.Exists(path))
                    {
                        File.Copy(path, $"{path}.backup_", true);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Print(e.Message);
                return false;
            }

            return true;
        }

        public static bool RestoreBackup(string path)
        {
            try
            {
                string backup = $"{path}.backup_";
                if (File.Exists(backup))
                {
                    File.Copy(backup, path, true);
                }
            }
            catch (Exception e)
            {
                Log.Print(e.Message);
                return false;
            }

            return true;
        }

        public static bool RestoreBackup(string[] arr)
        {
            try
            {
                foreach (string path in arr)
                {
                    string backup = $"{path}.backup_";
                    if (File.Exists(backup))
                    {
                        File.Copy(backup, path, true);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Print(e.Message);
                return false;
            }

            return true;
        }

        public static bool DeleteBackup(string path)
        {
            try
            {
                string backup = $"{path}.backup_";
                if (File.Exists(backup))
                {
                    File.Delete(backup);
                }
            }
            catch (Exception e)
            {
                Log.Print(e.Message);
                return false;
            }

            return true;
        }

        public static bool DeleteBackup(string[] arr)
        {
            try
            {
                foreach (string path in arr)
                {
                    string backup = $"{path}.backup_";
                    if (File.Exists(backup))
                    {
                        File.Delete(backup);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Print(e.Message);
                return false;
            }

            return true;
        }

        public enum MachineType : ushort
        {
            IMAGE_FILE_MACHINE_UNKNOWN = 0x0,
            IMAGE_FILE_MACHINE_AM33 = 0x1d3,
            IMAGE_FILE_MACHINE_AMD64 = 0x8664,
            IMAGE_FILE_MACHINE_ARM = 0x1c0,
            IMAGE_FILE_MACHINE_EBC = 0xebc,
            IMAGE_FILE_MACHINE_I386 = 0x14c,
            IMAGE_FILE_MACHINE_IA64 = 0x200,
            IMAGE_FILE_MACHINE_M32R = 0x9041,
            IMAGE_FILE_MACHINE_MIPS16 = 0x266,
            IMAGE_FILE_MACHINE_MIPSFPU = 0x366,
            IMAGE_FILE_MACHINE_MIPSFPU16 = 0x466,
            IMAGE_FILE_MACHINE_POWERPC = 0x1f0,
            IMAGE_FILE_MACHINE_POWERPCFP = 0x1f1,
            IMAGE_FILE_MACHINE_R4000 = 0x166,
            IMAGE_FILE_MACHINE_SH3 = 0x1a2,
            IMAGE_FILE_MACHINE_SH3DSP = 0x1a3,
            IMAGE_FILE_MACHINE_SH4 = 0x1a6,
            IMAGE_FILE_MACHINE_SH5 = 0x1a8,
            IMAGE_FILE_MACHINE_THUMB = 0x1c2,
            IMAGE_FILE_MACHINE_WCEMIPSV2 = 0x169,
        }

        public static MachineType GetDllMachineType(string dllPath)
        {
            FileStream fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            fs.Seek(0x3c, SeekOrigin.Begin);
            int peOffset = br.ReadInt32();
            fs.Seek(peOffset, SeekOrigin.Begin);
            uint peHead = br.ReadUInt32();

            if (peHead != 0x00004550) // "PE\0\0", little-endian
{
                throw new Exception("Can't find PE header");
            }

            MachineType machineType = (MachineType)br.ReadUInt16();
            br.Close();
            fs.Close();
            return machineType;
        }

        public static bool? UnmanagedDllIs64Bit(string dllPath)
        {
            switch (GetDllMachineType(dllPath))
            {
                case MachineType.IMAGE_FILE_MACHINE_AMD64:
                case MachineType.IMAGE_FILE_MACHINE_IA64:
                    return true;
                case MachineType.IMAGE_FILE_MACHINE_I386:
                    return false;
                default:
                    return null;
            }
        }
    }
}
