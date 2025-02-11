using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using System.Diagnostics;

class Program
{
    static async Task Main(string[] args)
    {
        // Lua LSP 服务器路径
        var serverPath = @"C:\bin\lua-language-server.exe";
        var workspacePath = @"C:\testLua"; // 工作空间路径
        var fileName = "lspTest.lua";

        // 创建 LSP 服务器进程
        var processStartInfo = new ProcessStartInfo
        {
            FileName = serverPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true, // 添加错误输出重定向
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process
        {
            StartInfo = processStartInfo
        };

        // 添加错误输出处理
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine($"Server Error: {e.Data}");
        };

        process.Start();
        process.BeginErrorReadLine(); // 开始异步读取错误输出

        // 创建 LSP 客户端
        var client = LanguageClient.PreInit(options =>
{
    // 配置客户端选项
    options
        .WithInput(process.StandardOutput.BaseStream)
        .WithOutput(process.StandardInput.BaseStream)
        .WithRootPath(workspacePath) // 设置工作空间根路径
        .WithTrace(InitializeTrace.Verbose) // 设置跟踪级别
        .WithLoggerFactory(LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        }))
        // 使用 OmniSharp 内置的处理程序
        .OnLogMessage(logMessage =>
        {
            Console.WriteLine($"Log Message: {logMessage.Message}");
            return Task.CompletedTask;
        })
         .OnShowMessage(showMessage =>
         {
             Console.WriteLine($"Show Message: {showMessage.Message}");
             return Task.CompletedTask;
         })
         .OnUnregisterCapability(unregisterCapability =>
         {
             Console.WriteLine($"Unregister Capability: {unregisterCapability.Unregistrations}");
             return Task.CompletedTask;
         })
         .OnNotification("$/hello", (JToken parameters) =>
         {
             Console.WriteLine($"Received $/hello notification with parameters: {parameters}");
             return Task.CompletedTask;
         })
         .OnProgress((progress, token) =>
         {
             Console.WriteLine($"Received progress notification: {progress}");
             return Task.CompletedTask;
         })
         .OnNotification("$/progress", (JToken parameters) =>
         {
             Console.WriteLine($"Received $/progress notification with parameters: {parameters}");
             return Task.CompletedTask;
         })
        .OnLogTrace(logTrace =>
        {
            Console.WriteLine($"logTrace:{logTrace.Message}");
        })


        .AddHandler<CustomMessageHandler>() // 注册自定义消息处理程序
        .AddHandler<HelloNotificationHandler>() // 注册 hello 通知处理程序
        .AddHandler<LogMessageHandler>(); // 注册 logMessage 通知处理程序
                                          //options.WithCapability(
                                          //new RenameCapability
                                          //{
                                          //    PrepareSupport = true,
                                          //    PrepareSupportDefaultBehavior = PrepareSupportDefaultBehavior.Identifier
                                          //},
                                          //new HoverCapability
                                          //{
                                          //    ContentFormat = new Container<MarkupKind>(MarkupKind.Markdown, MarkupKind.PlainText)
                                          //},
                                          //new DefinitionCapability(),
                                          //new ReferenceCapability(),
                                          //new DocumentFormattingCapability(),
                                          //new DocumentSymbolCapability
                                          //{
                                          //    HierarchicalDocumentSymbolSupport = true
                                          //},
                                          //new CompletionCapability()
                                          //,
                                          //new SemanticTokensCapability(
                                          //    ),
                                          //new TextSynchronizationCapability
                                          //{
                                          //    WillSave = true,
                                          //    WillSaveWaitUntil = true,
                                          //    DidSave = true
                                          //},
                                          //new DiagnosticClientCapabilities()

    //    );


    // 配置初始化选项
    options.WithInitializationOptions(new Dictionary<string, object>
    {
        ["workspace"] = new Dictionary<string, object>
        {
            ["library"] = new string[] { }, // 可以添加Lua库路径
            ["checkThirdParty"] = false
        },
        ["runtime"] = new Dictionary<string, object>
        {
            ["version"] = "Lua 5.4", // 指定Lua版本
            ["path"] = new string[]
            {
                "?.lua",
                "?/init.lua",
                "?/?.lua"
            }
        },
        ["diagnostics"] = new Dictionary<string, object>
        {
            ["enable"] = true,
            ["globals"] = new string[] { } // 可以添加全局变量
        }
    });
});

        Console.WriteLine("Lua LSP Client starting...");
        int col = 0, row = 1;
        try
        {
            // 初始化客户端
            await client.Initialize(default);

            // 创建文档
            var documentItem = new TextDocumentItem
            {
                Uri = DocumentUri.From(Path.Combine(workspacePath, fileName)),
                LanguageId = "lua",
                Version = 2,
                Text = File.ReadAllText(Path.Combine(workspacePath, fileName)),
            };

            // 打开文档
            client.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams
            {
                TextDocument = documentItem
            });

            Console.WriteLine("Test document opened. Waiting for server response...");

            // 等待服务器处理
            await Task.Delay(1000);


            // 请求代码补全
            var completionParams = new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier(documentItem.Uri),
                Position = new Position(col, row) // 在第3行第11列请求补全
            };
            var sigRes = await client.TextDocument.RequestSignatureHelp(new SignatureHelpParams
            {
                TextDocument = new TextDocumentIdentifier(documentItem.Uri),
                Position = new Position(1, 8)
            });
            if (sigRes != null)
            {
                Console.WriteLine($"Signature Help: {sigRes.Signatures.Index}");
            }

            var completions = await client.TextDocument.RequestCompletion(completionParams);
            if (completions != null)
            {
                Console.WriteLine("\nCompletion items:");
                foreach (var item in completions)
                {
                    Console.WriteLine($"- {item.Label} ({item.Kind}) {item.Detail}");
                }
            }
            // 1. 添加 Hover 功能
            Console.WriteLine("\nTesting Hover...");
            var hoverParams = new HoverParams
            {
                // TextDocument = "c:/Users/yupeng.zhang/Documents/testLua/__sys.lua",
                TextDocument = new TextDocumentIdentifier(documentItem.Uri),
                Position = new Position(col, row)
            };

            var hover = await client.TextDocument.RequestHover(hoverParams);
            if (hover != null)
            {
                Console.WriteLine($"Hover Result: {hover.Contents}");
            }

            // 2. 添加转到定义功能
            Console.WriteLine("\nTesting Go to Definition...");
            var definitionParams = new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier(documentItem.Uri),
                Position = new Position(col, row)
            };

            var definitions = await client.TextDocument.RequestDefinition(definitionParams);
            if (definitions != null)
            {
                foreach (LocationOrLocationLink location in definitions)
                {
                    Console.WriteLine($"Definition found at: {location.Location.Uri}, " +
                        $"Range: {location.Location.Range.Start.Line}:{location.Location.Range.Start.Character} - " +
                        $"{location.Location.Range.End.Line}:{location.Location.Range.End.Character}");
                }
            }

            // 3. 添加查找引用功能
            Console.WriteLine("\nTesting Find References...");
            var referencesParams = new ReferenceParams
            {
                TextDocument = new TextDocumentIdentifier(documentItem.Uri),
                Position = new Position(col, row),
                Context = new ReferenceContext { IncludeDeclaration = true }
            };

            var references = await client.TextDocument.RequestReferences(referencesParams);
            if (references != null)
            {
                foreach (var reference in references)
                {
                    Console.WriteLine($"Reference found at: {reference.Uri}, " +
                        $"Range: {reference.Range.Start.Line}:{reference.Range.Start.Character}");
                }
            }

            // 4. 添加文档符号功能
            Console.WriteLine("\nTesting Document Symbols...");
            var documentSymbolParams = new DocumentSymbolParams
            {
                TextDocument = new TextDocumentIdentifier(documentItem.Uri)
            };

            var symbols = await client.TextDocument.RequestDocumentSymbol(documentSymbolParams);
            if (symbols != null)
            {
                foreach (var symbol in symbols)
                {
                    if (symbol.IsDocumentSymbolInformation)
                    {
                        var symbolInfo = symbol.SymbolInformation;
                        Console.WriteLine($"Symbol: {symbolInfo.Name}, Kind: {symbolInfo.Kind}, " +
                            $"Location: {symbolInfo.Location.Range.Start.Line}:{symbolInfo.Location.Range.Start.Character}");
                    }
                    else if (symbol.IsDocumentSymbol)
                    {
                        var documentSymbol = symbol.DocumentSymbol;
                        Console.WriteLine($"Symbol: {documentSymbol.Name}, Kind: {documentSymbol.Kind}, " +
                            $"Range: {documentSymbol.Range.Start.Line}:{documentSymbol.Range.Start.Character}");
                    }
                }
            }

            // 5. 添加格式化功能
            Console.WriteLine("\nTesting Document Formatting...");
            var formatParams = new DocumentFormattingParams
            {
                TextDocument = new TextDocumentIdentifier(documentItem.Uri),
                Options = new FormattingOptions
                {
                    TabSize = 4,
                    InsertSpaces = true,
                    TrimTrailingWhitespace = true,
                    InsertFinalNewline = true,
                    TrimFinalNewlines = true

                }
            };

            var textEdits = await client.TextDocument.RequestDocumentFormatting(formatParams);
            if (textEdits != null)
            {
                foreach (var edit in textEdits)
                {
                    Console.WriteLine($"Format edit: Range: {edit.Range}, NewText: {edit.NewText}");
                }
            }

            // 6. 添加重命名功能
            Console.WriteLine("\nTesting Rename...");
            var renameParams = new RenameParams
            {
                TextDocument = new TextDocumentIdentifier(documentItem.Uri),
                Position = new Position(col, row),
                NewName = "newName"
            };

            var workspaceEdit = await client.TextDocument.RequestRename(renameParams);
            if (workspaceEdit?.Changes != null)
            {
                foreach (var change in workspaceEdit.Changes)
                {
                    Console.WriteLine($"Rename changes in file {change.Key}:");
                    foreach (var edit in change.Value)
                    {
                        Console.WriteLine($"- Range: {edit.Range}, NewText: {edit.NewText}");
                    }
                }
            }
            Console.WriteLine("颜色替换");

            var color = await client.TextDocument.RequestColorPresentation(new ColorPresentationParams
            {
                TextDocument = new TextDocumentIdentifier(documentItem.Uri),
                Color = new DocumentColor
                {
                    Red = 1,
                    Green = 0,
                    Blue = 0,
                    Alpha = 1.0f
                },
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                {
                    Start = new Position(9, 0),
                    End = new Position(9, 6)
                }
            });
            if (color != null)
            {
                foreach (var item in color)
                {
                    Console.WriteLine($"Color: {item.Label}");
                }
            }
            //Console.WriteLine("请求某个位置的声明信息");
            //var declarationParams = new DeclarationParams
            //{
            //    TextDocument = new TextDocumentIdentifier(documentItem.Uri),
            //    Position = new Position(1, 8)
            //};

            //var declarations = await client.TextDocument.RequestDeclaration(declarationParams);
            //if (declarations != null)
            //{
            //    foreach (var declaration in declarations)
            //    {
            //        Console.WriteLine($"Declaration found at: {declaration.Location.Uri}, " +
            //            $"Range: {declaration.Location.Range.Start.Line}:{declaration.Location.Range.Start.Character} - " +
            //            $"{declaration.Location.Range.End.Line}:{declaration.Location.Range.End.Character}");
            //    }
            //}

            Console.WriteLine("颜色信息");
            var colors = await client.TextDocument.RequestDocumentColor(new DocumentColorParams
            {
                TextDocument = new TextDocumentIdentifier(documentItem.Uri)
            });
            if (colors != null)
            {
                foreach (var item in colors)
                {
                    Console.WriteLine($"Color: {item.Color.Red},{item.Color.Green},{item.Color.Blue},{item.Color.Alpha}");
                }
            }
            //代码高亮
            Console.WriteLine("代码高亮");
            SemanticTokensParams semanticTokensParams = new SemanticTokensParams
            {
                TextDocument = new TextDocumentIdentifier(documentItem.Uri)
            };
            var semanticTokens = await client.TextDocument.RequestSemanticTokensFull(semanticTokensParams);
            if (semanticTokens != null)
            {
                Console.WriteLine($"Semantic Tokens: {semanticTokens.ResultId}");
            }

            //7代码诊断
            Console.WriteLine("代码诊断");
            RelatedDocumentDiagnosticReport info = await client.RequestDocumentDiagnostic(new DocumentDiagnosticParams
            {
                TextDocument = new TextDocumentIdentifier(Path.Combine(workspacePath, fileName)),
                Identifier = "lua"
            }, new CancellationToken());
            foreach (var item in info?.RelatedDocuments)
            {
                Console.WriteLine($"Diagnostic: {item}");
            }


            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
        finally
        {
            try
            {
                // 关闭文档
                if (client.TextDocument != null)
                {
                    client.TextDocument.DidCloseTextDocument(new DidCloseTextDocumentParams
                    {
                        TextDocument = new TextDocumentIdentifier(DocumentUri.From(Path.Combine(workspacePath, fileName)))
                    });
                }

                // 正常关闭客户端
                await client.Shutdown();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Shutdown Error: {ex.Message}");
            }
            finally
            {
                // 确保进程被终止
                if (!process.HasExited)
                {
                    process.Kill();
                }
                process.Dispose();
            }
        }
    }
}

internal class CustomMessageHandler : IJsonRpcRequestHandler<CustomMessageParams, CustomMessageResponse>
{
    public Task<CustomMessageResponse> Handle(CustomMessageParams request, CancellationToken cancellationToken)
    {
        // 处理自定义消息
        var response = new CustomMessageResponse
        {
            Message = $"Received: {request.Message}"

        };
        Console.WriteLine($"############## Received: {request.Message}");
        return Task.FromResult(response);
    }
}

public class CustomMessageParams : IRequest<CustomMessageResponse>
{
    public string Message { get; set; }
}

public class CustomMessageResponse
{
    public string Message { get; set; }
}
[Method("$/hello")]
public class HelloNotificationHandler : IJsonRpcNotificationHandler<HelloNotification>
{
    public Task<Unit> Handle(HelloNotification request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Received hello notification: {request.Message}");
        return Unit.Task;
    }
}

public class HelloNotification : IRequest
{
    public string Message { get; set; }
}
[Method("window/logMessage")]
public class LogMessageHandler : IJsonRpcNotificationHandler<LogMessageParams>
{
    public Task<Unit> Handle(LogMessageParams request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Log message: {request.Message}");
        return Unit.Task;
    }
}

public class LogMessageParams : IRequest
{
    public string Message { get; set; }
}


