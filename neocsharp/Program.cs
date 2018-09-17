using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Neo.Compiler;
using Neo.Compiler.MSIL;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace neocsharp
{
    class Program
    {
        static void Main(string[] args)
        {
            //set console
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            var log = new DefLogger();
            log.Log("Neo.Compiler.C# console app v" + Assembly.GetEntryAssembly().GetName().Version);

            bool bCompatible = false;
            string filename = null;
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i][0] == '-')
                {
                    if (args[i] == "--compatible")
                    {
                        bCompatible = true;
                    }

                    //other option
                }
                else
                {
                    filename = args[i];
                }
            }

            if (filename == null)
            {
                log.Log("need one param for C# filename.");
                log.Log("[--compatible] disable nep8 function");
                log.Log("Example:neon abc.cs --compatible");
                return;
            }

            var srccode = System.IO.File.ReadAllText(filename);
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(srccode);

            var op = new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary);
            var ref1 = MetadataReference.CreateFromFile("needlib" + System.IO.Path.DirectorySeparatorChar + "mscorlib.dll");
            var ref2 = MetadataReference.CreateFromFile("needlib" + System.IO.Path.DirectorySeparatorChar + "System.dll");
            var ref3 = MetadataReference.CreateFromFile("needlib" + System.IO.Path.DirectorySeparatorChar + "System.Numerics.dll");
            var ref4 = MetadataReference.CreateFromFile("needlib" + System.IO.Path.DirectorySeparatorChar + "Neo.SmartContract.Framework.dll");
            var comp = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("aaa.dll", new[] { tree },
               new[] { ref1,ref2, ref3, ref4 }, op);

            var timestart = DateTime.Now;
            var fs = new System.IO.MemoryStream();
            var fspdb = new System.IO.MemoryStream();
            var result = comp.Emit(fs, fspdb);
            var timeend = DateTime.Now;

            log.Log(">>compile time:" + (timeend - timestart).TotalSeconds + "s");
            foreach (var c in result.Diagnostics)
            {
                log.Log(c.ToString());
            }
            if (result.Success==false)
            {
                log.Log(">>compile error");
                Environment.Exit(-1);
            }
            else
            {
                log.Log(">>compile finish");
            }
            fs.Seek(0, System.IO.SeekOrigin.Begin);
            fspdb.Seek(0, System.IO.SeekOrigin.Begin);

            if (bCompatible)
            {
                log.Log("use --compatible no nep8");
            }
            string onlyname = System.IO.Path.GetFileNameWithoutExtension(filename);
            string filepdb = onlyname + ".pdb";
            //set build path
            var buildpath = Path.GetFullPath("needlib");
            if (!string.IsNullOrEmpty(buildpath))
            {
                try
                {
                    Directory.SetCurrentDirectory(buildpath);
                    //mono.cecil need load dll from here.
                }
                catch
                {
                    log.Log("Could not find path: " + buildpath);
                    Environment.Exit(-1);
                }
            }

            ILModule mod = new ILModule();
            //System.IO.Stream fs = null;
            //System.IO.Stream fspdb = null;

            //open file
            //try
            //{
            //    fs = System.IO.File.OpenRead(filename);

            //    if (System.IO.File.Exists(filepdb))
            //    {
            //        fspdb = System.IO.File.OpenRead(filepdb);
            //    }

            //}
            //catch (Exception err)
            //{
            //    log.Log("Open File Error:" + err.ToString());
            //    return;
            //}
            //load module
            try
            {
                mod.LoadModule(fs, fspdb);
            }
            catch (Exception err)
            {
                log.Log("LoadModule Error:" + err.ToString());
                return;
            }
            byte[] bytes = null;
            bool bSucc = false;
            string jsonstr = null;
            //convert and build
            try
            {
                var conv = new ModuleConverter(log);
                ConvOption option = new ConvOption();
                option.useNep8 = !bCompatible;
                NeoModule am = conv.Convert(mod, option);
                bytes = am.Build();
                log.Log("convert succ");


                try
                {
                    var outjson = vmtool.FuncExport.Export(am, bytes);
                    StringBuilder sb = new StringBuilder();
                    outjson.ConvertToStringWithFormat(sb, 0);
                    jsonstr = sb.ToString();
                    log.Log("gen abi succ");
                }
                catch (Exception err)
                {
                    log.Log("gen abi Error:" + err.ToString());
                }

            }
            catch (Exception err)
            {
                log.Log("Convert Error:" + err.ToString());
                return;
            }

            //set out path
            var path = Path.GetDirectoryName(filename);
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    Directory.SetCurrentDirectory(path);
                }
                catch
                {
                    log.Log("Could not find path: " + path);
                    Environment.Exit(-1);
                }
            }
            //write bytes
            try
            {

                string bytesname = onlyname + ".avm";

                System.IO.File.Delete(bytesname);
                System.IO.File.WriteAllBytes(bytesname, bytes);
                log.Log("write:" + bytesname);
                bSucc = true;
            }
            catch (Exception err)
            {
                log.Log("Write Bytes Error:" + err.ToString());
                return;
            }
            try
            {

                string abiname = onlyname + ".abi.json";

                System.IO.File.Delete(abiname);
                System.IO.File.WriteAllText(abiname, jsonstr);
                log.Log("write:" + abiname);
                bSucc = true;
            }
            catch (Exception err)
            {
                log.Log("Write abi Error:" + err.ToString());
                return;
            }
            try
            {
                fs.Dispose();
                if (fspdb != null)
                    fspdb.Dispose();
            }
            catch
            {

            }

            if (bSucc)
            {
                log.Log("SUCC");
            }
        }
    }
}
