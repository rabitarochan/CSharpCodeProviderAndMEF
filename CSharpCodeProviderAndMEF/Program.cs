using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CSharp;

namespace CSharpCodeProviderAndMEF
{
    class Program
    {
        static void Main()
        {
            var codeFactory = new CodeFactory();
            var dllFactory = new DllFactory();
            var container = new MessageContainer();

            string preId = null;

            while (true) {
                Console.Write("文字を入力してください。> ");
                var message = Console.ReadLine();

                var sw = Stopwatch.StartNew();

                if (string.IsNullOrEmpty(message)) {
                    var preInstance = container.Resolve(preId);
                    Console.WriteLine(
                        "cached MEF. [Message: {0}, Time: {1}]",
                        preInstance.GetMessage(),
                        sw.Elapsed);
                    continue;
                }

                var id = CreateId();
                var code = codeFactory.CreateCode(id, message);

                var compileResult = dllFactory.Compile(id, code);

                if (compileResult.NativeCompilerReturnValue != 0) {
                    Console.Error.WriteLine(string.Concat(compileResult.Output.Cast<string>()));
                    continue;
                }

                container.Refresh();
                var instance = container.Resolve(id);
                Console.WriteLine(
                    "from MEF. [Message: {0}, Time: {1}]",
                    instance.GetMessage(),
                    sw.Elapsed);

                preId = id;
            }
        }

        static string CreateId()
        {
            var id = Guid.NewGuid().ToString("N");
            return "_" + id;
        }
    }

    public class MessageModel
    {
        public string Id { get; set; }
        public string Message { get; set; }
    }

    public interface IMessage
    {
        string GetMessage();
    }

    public class CodeFactory
    {
        private const string TemplatePath = @"App_Data\IMessageTemplate.razor";
        private const string CacheName = "message";

        public CodeFactory()
        {
            Initialize();
        }

        public string CreateCode(string id, string message)
        {
            var code = RazorEngine.Razor.Run(CacheName, new MessageModel { Id = id, Message = message });
            return code;
        }


        // private

        private void Initialize()
        {
            var template = File.ReadAllText(TemplatePath);
            RazorEngine.Razor.Compile<MessageModel>(template, CacheName);
        }
    }

    public class DllFactory
    {
        public const string ExtensionDirectoryPath = @"App_Data\Extensions";

        private CSharpCodeProvider csc;

        public DllFactory()
        {
            Initialize();
        }

        public CompilerResults Compile(string id, string code)
        {
            var parameter = CreateParameter(id);
            var result = csc.CompileAssemblyFromSource(parameter, code);

            return result;
        }


        // private

        private void Initialize()
        {
            if (!Directory.Exists(ExtensionDirectoryPath)) {
                Directory.CreateDirectory(ExtensionDirectoryPath);
            }

            csc = new CSharpCodeProvider(new Dictionary<string, string> {
                { "CompilerVersion", "v4.0" }
            });
        }

        private CompilerParameters CreateParameter(string id)
        {
            var dllPath = Path.Combine(ExtensionDirectoryPath, id + ".dll");

            var parameter = new CompilerParameters(new[] {
                "mscorlib.dll",
                "System.dll",
                "System.Core.dll",
                "System.ComponentModel.Composition.dll",
                "CSharpCodeProviderAndMEF.exe" // 自分を含めるのを忘れずに!!
            }, dllPath);

            return parameter;
        }
    }

    class MessageContainer
    {
        private DirectoryCatalog dirCatalog;
        private CompositionContainer container;

        public MessageContainer()
        {
            Initialize();
        }

        public IMessage Resolve(string id)
        {
            var instance = container.GetExportedValue<IMessage>(id);
            return instance;
        }

        public void Refresh()
        {
            dirCatalog.Refresh();
        }


        // private

        private void Initialize()
        {
            var asmCatalog = new AssemblyCatalog(System.Reflection.Assembly.GetExecutingAssembly());
            dirCatalog = new DirectoryCatalog(DllFactory.ExtensionDirectoryPath);
            var catalog = new AggregateCatalog(asmCatalog, dirCatalog);

            container = new CompositionContainer(catalog);
        }
    }
}
