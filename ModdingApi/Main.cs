using System;
using System.IO;
using System.Reflection;

namespace ModdingApi {
    class Loader {
        public static void Main() {
            try {
                Assembly mod = Assembly.LoadFile("D:\\Program Files (x86)\\Steam\\steamapps\\common\\Factory Town\\Mods\\huge_silo\\HugeSilo.dll");
                AppDomain.CurrentDomain.Load(mod.GetName());
                Type mainType = mod.GetType("HugeSilo.Main");

                if(mainType != null) {
                    MethodInfo loadMethod = mainType.GetMethod("Load");
                    if(loadMethod != null) {
                        loadMethod.Invoke(null, null);
                    } else {
                        throw new FileLoadException("Cannot find Load Method.");
                    }
                } else {
                    throw new FileLoadException("Cannot find Main class.");
                }

        } catch(Exception ex){
                using(StreamWriter writer = new StreamWriter("modding_logs\\modding_error_file.txt")) {
                    writer.WriteLine("-----------------------------------------------------------------------------");
                    writer.WriteLine("Date : " + DateTime.Now.ToString());
                    writer.WriteLine();

                    while(ex != null) {
                        writer.WriteLine(ex.GetType().FullName);
                        writer.WriteLine("Message : " + ex.Message);
                        writer.WriteLine("StackTrace : " + ex.StackTrace);

                        ex = ex.InnerException;
                    }
                }
                throw;
            }
        }
    }
}
