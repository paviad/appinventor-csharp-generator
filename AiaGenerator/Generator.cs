using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;

namespace AiaGenerator {
    [Generator]
    public class Generator : ISourceGenerator {
        private readonly List<List<string>> _localFunctions = new List<List<string>>();

        private readonly Dictionary<string, string> _symbolTable = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _symbolTableReplacements = new Dictionary<string, string>();

        private int _lfCount;

        public void Initialize(GeneratorInitializationContext context) {
        }

        public void Execute(GeneratorExecutionContext context) {
            var aiaFiles = context.AdditionalFiles.Where(file => file.Path.EndsWith(".aia"));
            foreach (var aiaFile in aiaFiles) {
                var source = string.Join("\n", Read(aiaFile.Path).Split('\n').Indent().Indent());
                var className = Path.GetFileNameWithoutExtension(aiaFile.Path);
                var source2 = $"namespace Testa {{\n" +
                              $"    [Aia]\n" +
                              $"    public partial class {className} {{\n" +
                              $"{source}\n" +
                              $"    }}\n" +
                              $"}}\n";

                var fn = Path.GetFileName(aiaFile.Path) + ".cs";
                context.AddSource(fn, source2.Replace("\r\n", "\n").Replace("\n", "\r\n"));
            }

            var attrSource = $"namespace Testa {{\n" +
                             $"    [AttributeUsage(AttributeTargets.Class)]\n" +
                             $"    public class AiaAttribute : Attribute {{\n" +
                             $"    }}\n" +
                             $"}}\n";
            context.AddSource("AiaAttribute.cs", attrSource.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        }

        public string Read(string aiaPath) {
            _lfCount = 1;

            _symbolTable["Move@x"] = "int";
            _symbolTable["Move@y"] = "int";
            _symbolTable["Detect@Template"] = "string";
            _symbolTable["Click@Location"] = "string";
            _symbolTable["Detect"] = "bool";

            using (var zipStream = File.Open(aiaPath, FileMode.Open, FileAccess.Read)) {
                var zip = new ZipArchive(zipStream);
                var xml = zip.Entries.First(x => x.Name.EndsWith("bky"));
                using (var stream = xml.Open()) {
                    var xmlSerializer = new XmlSerializer(typeof(AiaProgram),
                        defaultNamespace: "http://www.w3.org/1999/xhtml");
                    var aiaProgram = (AiaProgram)xmlSerializer.Deserialize(stream);
                    var rc1 = EmitProgram(aiaProgram).Item1;
                    var rc = string.Join("\n", rc1.Semicolon());
                    foreach (var kv in _symbolTableReplacements) {
                        var tmp = kv.Key;
                        var real = _symbolTable[kv.Value];
                        rc = rc.Replace(tmp, real);
                    }

                    foreach (var kv in _symbolTable) {
                        if (!Guid.TryParse(kv.Value, out var gg)) {
                            continue;
                        }

                        rc = rc.Replace(kv.Value, "object");
                    }

                    return rc;
                }
            }
        }

        private static List<string> Todo(string s) => new List<string>(new[] { $"... ({s})" });

        private int AddLocalFunction(IEnumerable<string> sb, string type) {
            var sb2 = new List<string>();
            var lfid = _lfCount++;
            sb2.Add($"async Task<{type}> LocalFunction{lfid}() {{");
            sb2.AddRange(sb);
            sb2.Add("}");
            _localFunctions.Add(sb2);
            return lfid;
        }

        private (List<string>, string) EmitControlsChoose(AiaBlock block) {
            var sb = new List<string>();

            var cond = EmitProgramBlock(block.Values.Single(r => r.Name == "TEST").Block).Item1.Single();
            var programBlock = EmitProgramBlock(block.Values.Single(r => r.Name == "THENRETURN").Block);
            var iftrue = programBlock.Item1.Single();
            var iffalse = EmitProgramBlock(block.Values.Single(r => r.Name == "ELSERETURN").Block).Item1.Single();

            sb.Add($"{cond} ? {iftrue} : {iffalse}");
            var type = programBlock.Item2;
            return (sb, type);
        }

        private (List<string>, string) EmitControlsDoThenReturn(AiaBlock block) {
            var sb = new List<string>();

            var body = block.Statements.Single(r => r.Name == "STM").Block;
            var statements = EmitProgramBlock(body);
            sb.AddRange(statements.Item1.Indent());
            var value = block.Values.Single(r => r.Name == "VALUE").Block;
            var val = EmitProgramBlock(value);
            sb.Add($"    return {val.Item1.Single()}");

            var type = val.Item2;
            var lfid = AddLocalFunction(sb, type);

            var sb2 = new List<string> {
                $"await LocalFunction{lfid}()",
            };

            return (sb2, type);
        }

        private (List<string>, string) EmitControlsIf(AiaBlock block) {
            var sb = new List<string>();

            var num = block.Mutation.Else;

            for (var i = 0; i < num; i++) {
                var cond = EmitProgramBlock(block.Values.Single(r => r.Name == $"IF{i}").Block).Item1.Single();
                var bodyBlock = EmitProgramBlock(block.Statements.Single(r => r.Name == $"DO{i}").Block);

                if (i == 0) {
                    sb.Add($"if ({cond}) {{");
                }
                else {
                    sb.Add("}");
                    sb.Add($"else if ({cond}) {{");
                }

                sb.AddRange(bodyBlock.Item1.Select(z => $"    {z}"));
                sb.Add("}");
            }

            if (num > 0) {
                var bodyBlock = EmitProgramBlock(block.Statements.Single(r => r.Name == "ELSE").Block);

                sb.Add("else {");

                sb.AddRange(bodyBlock.Item1.Select(z => $"    {z}"));
                sb.Add("}");
            }

            var lf = GetLocalFunctions();
            sb.InsertRange(0, lf);

            return (sb, "@@@");
        }

        private (List<string>, string) EmitControlsWhile(AiaBlock block) {
            var sb = new List<string>();
            var testBlock = block.Values.Single(r => r.Name == "TEST").Block;
            var test = EmitProgramBlock(testBlock);
            sb.Add($"while ({test.Item1.Single()}) {{");

            var bodyBlock = block.Statements.Single(r => r.Name == "DO").Block;
            var body = EmitProgramBlock(bodyBlock);

            sb.AddRange(body.Item1.Select(r => $"    {r}"));
            sb.Add("}");

            var lf = GetLocalFunctions();
            sb.InsertRange(0, lf);

            return (sb, "@@@");
        }

        private (List<string>, string) EmitLexicalVariableGet(AiaBlock block) {
            var sb = new List<string>();
            var val = block.Fields.Single(r => r.Name == "VAR").Value;
            sb.Add(val);
            var type = GetFromSymbolTable(val);
            return (sb, type);
        }

        private (List<string>, string) EmitLexicalVariableSet(AiaBlock block) {
            var sb = new List<string>();

            var varName = block.Fields.Single(r => r.Name == "VAR").Value;
            var valBlock = block.Values.Single().Block;
            var val = EmitProgramBlock(valBlock).Item1.Single();

            sb.Add($"{varName} = {val}");

            return (sb, "@@@");
        }

        private (List<string>, string) EmitListsCreateWith(AiaBlock block) {
            var sb = new List<string>();

            if (block.Values != null) {
                var elements = block.Values.SelectMany(r => EmitProgramBlock(r.Block).Item1);

                var lst = string.Join(", ", elements);

                sb.Add(lst);
            }

            return (sb, "params object[]");
        }

        private (List<string>, string) EmitLocalDeclarationExpression(AiaBlock block) {
            var sb = new List<string>();

            List<string> GetLocals() {
                var sb2 = new List<string>();

                var localnames = block.Mutation?.AiaLocalnames ?? Array.Empty<AiaLocalname>();
                for (var i = 0; i < localnames.Length; i++) {
                    var decl = block.Values.Single(r => r.Name == $"DECL{i}");
                    string type;
                    var init = "";
                    switch (decl.Block.Type) {
                        case "logic_boolean":
                            type = "bool";
                            init = $" = {(decl.Block.Fields.Single().Value == "TRUE" ? "true" : "false")}";
                            break;
                        case "math_number":
                            type = "int";
                            init = $" = {decl.Block.Fields.Single().Value}";
                            break;
                        default:
                            type = "object";
                            break;
                    }

                    var name = localnames[i].Name;

                    sb2.Add($"{type} {name}{init}");

                    _symbolTable[name] = type;
                }

                return sb2;
            }

            sb.AddRange(GetLocals().Indent());

            var insertion = sb.Count;

            var body = block.Values.Single(r => r.Name == "RETURN").Block;
            var programBlock = EmitProgramBlock(body);
            var type1 = programBlock.Item2;
            var statements = programBlock.Item1.Single();

            sb.Add($"    return {statements}");

            var lf = GetLocalFunctions();
            sb.InsertRange(insertion, lf.Indent());

            var lfid = AddLocalFunction(sb, type1);

            var sb3 = new List<string> {
                $"await LocalFunction{lfid}()",
            };

            return (sb3, type1);
        }

        private (List<string>, string) EmitLogicBoolean(AiaBlock block) {
            var sb = new List<string>();

            string val;
            switch (block.Fields.Single().Value) {
                case "TRUE":
                    val = "true";
                    break;
                case "FALSE":
                    val = "false";
                    break;
                default:
                    val = "@@";
                    break;
            }

            sb.Add($"{val}");

            return (sb, "bool");
        }

        private (List<string>, string) EmitLogicCompare(AiaBlock block) {
            var sb = new List<string>();

            string op;
            switch (block.Fields.Single(x => x.Name == "OP").Value) {
                case "EQ":
                    op = "==";
                    break;
                case "NEQ":
                    op = "!=";
                    break;
                default:
                    op = "@@";
                    break;
            }

            var terms = block.Values.Select(z => EmitProgramBlock(z.Block).Item1.Single());
            var t = string.Join($" {op} ", terms);
            sb.Add($"({t})");

            return (sb, "bool");
        }

        private (List<string>, string) EmitLogicNegate(AiaBlock block) {
            var sb = new List<string>();

            var body = EmitProgramBlock(block.Values.Single().Block);

            sb.Add($"!({body.Item1.Single()})");

            return (sb, "bool");
        }

        private (List<string>, string) EmitLogicOperation(AiaBlock block) {
            var sb = new List<string>();
            string op;
            switch (block.Fields.Single(x => x.Name == "OP").Value) {
                case "AND":
                    op = "&&";
                    break;
                case "OR":
                    op = "||";
                    break;
                default:
                    op = "@@";
                    break;
            }

            var terms = block.Values.Select(z => EmitProgramBlock(z.Block).Item1.Single());
            var t = string.Join($" {op} ", terms);
            sb.Add($"({t})");

            return (sb, "bool");
        }

        private (List<string>, string) EmitMathAdd(AiaBlock block) {
            var sb = new List<string>();

            var terms = block.Values.Select(r => EmitProgramBlock(r.Block).Item1.Single());
            var t = string.Join(" + ", terms.Select(r => $"({r})"));

            sb.Add($"{t}");

            return (sb, "int");
        }

        private (List<string>, string) EmitMathNumber(AiaBlock block) {
            var sb = new List<string>();

            var value = block.Fields.Single().Value;
            sb.Add($"{value}");

            return (sb, "int");
        }

        private (List<string>, string) EmitProceduresCallnoreturn(AiaBlock block) {
            var sb = new List<string>();

            var funcName = block.Fields.Single(r => r.Name == "PROCNAME").Value;
            var argNames = block.Mutation.AiaArgs.Select((r, i) => (r, i))
                .ToDictionary(r => $"ARG{r.i}", r => r.r.Name);
            var args1 = block.Values.Where(r => r.Name.StartsWith("ARG"))
                .Select(r => (r.Name, EmitProgramBlock(r.Block))).ToList();
            var args = args1.SelectMany(r => r.Item2.Item1).ToList();
            foreach (var arg in args1) {
                _symbolTable[$"{funcName}@{argNames[arg.Name]}"] = arg.Item2.Item2;
            }

            var argsAgg = string.Join(", ", args);

            if (funcName.StartsWith("Invoke")) {
                var libName = args[0].Substring(1, args[0].Length - 2);
                var argsAgg2 = string.Join(", ", args.Skip(1));
                sb.Add($"await Invoke_{libName}({argsAgg2})");
            }
            else {
                sb.Add($"await {funcName}({argsAgg})");
            }

            var type = GetFromSymbolTable(funcName);

            return (sb, type);
        }

        private (List<string>, string) EmitProceduresCallreturn(AiaBlock block) {
            var sb = new List<string>();

            var funcName = block.Fields.Single(r => r.Name == "PROCNAME").Value;
            var argNames = block.Mutation.AiaArgs.Select((r, i) => (r, i))
                .ToDictionary(r => $"ARG{r.i}", r => r.r.Name);
            var args1 = block.Values.Where(r => r.Name.StartsWith("ARG"))
                .Select(r => (r.Name, EmitProgramBlock(r.Block))).ToList();
            var args = args1.Select(r => r.Item2.Item1.Single()).ToList();
            foreach (var arg in args1) {
                _symbolTable[$"{funcName}@{argNames[arg.Name]}"] = arg.Item2.Item2;
            }

            var argsAgg = string.Join(", ", args);

            string type;
            if (funcName.StartsWith("Invoke")) {
                var libName = args[0].Substring(1, args[0].Length - 2);
                var argsAgg2 = string.Join(", ", args.Skip(1));
                sb.Add($"await Invoke_{libName}({argsAgg2})");
                type = _symbolTable[libName];
            }
            else {
                sb.Add($"await {funcName}({argsAgg})");
                type = GetFromSymbolTable(funcName);
            }

            return (sb, type);
        }

        private (List<string>, string) EmitProceduresDefNoReturn(AiaBlock block) {
            var sb = new List<string>();
            var methodName = block.Fields.Single(r => r.Name == "NAME").Value;

            if (methodName.StartsWith("Invoke")) {
                return (sb, "@@@");
            }

            var paramNames = block.Fields.Where(r => r.Name.StartsWith("VAR")).Select(r => r.Value);
            var paramsWithTypes = paramNames.Select(r => {
                var argType = GetFromSymbolTable($"{methodName}@{r}");
                return $"{argType} {r}";
            });
            var parameters = string.Join(", ", paramsWithTypes);

            sb.Add($"public async Task {methodName}({parameters}) {{");

            var bodyBlock = block.Statements?.Single().Block;
            if (bodyBlock != null) {
                var programBlock = EmitProgramBlock(bodyBlock);
                var body = programBlock.Item1;

                sb.AddRange(body.Indent());
            }

            sb.Add("}");
            sb.Add("");

            var lf = GetLocalFunctions();
            sb.InsertRange(1, lf.Indent());

            return (sb, "@@@");
        }

        private (List<string>, string) EmitProceduresDefReturn(AiaBlock block) {
            var sb = new List<string>();
            var methodName = block.Fields.Single(r => r.Name == "NAME").Value;

            if (methodName.StartsWith("Invoke")) {
                return (sb, "@@@");
            }

            var paramNames = block.Fields.Where(r => r.Name.StartsWith("VAR")).Select(r => r.Value);
            var paramsWithTypes = paramNames.Select(r => {
                var argType = GetFromSymbolTable($"{methodName}@{r}");
                return $"{argType} {r}";
            });
            var parameters = string.Join(", ", paramsWithTypes);

            var returnBlock = block.Values.Single(r => r.Name == "RETURN").Block;

            var programBlock = EmitProgramBlock(returnBlock);
            var body = programBlock.Item1.Single();
            var retVal = programBlock.Item2;

            sb.Add($"public async Task<{retVal}> {methodName}({parameters}) {{");
            sb.Add($"    return {body}");
            sb.Add("}");
            sb.Add("");

            var lf = GetLocalFunctions();
            sb.InsertRange(1, lf.Indent());

            _symbolTable[methodName] = retVal;

            return (sb, "@@@");
        }

        private (List<string>, string) EmitProgram(AiaProgram aiaProgram) {
            var sb = new List<string>();
            var type = "@@@@";
            foreach (var block in aiaProgram.Blocks) {
                var programBlock = EmitProgramBlock(block);
                type = programBlock.Item2;
                var str = programBlock.Item1;
                sb.AddRange(str);
            }

            return (sb, type);
        }

        private (List<string>, string) EmitProgramBlock(AiaBlock block) {
            var type = "@@@@@@";
            var sb = new List<string>();
            (List<string>, string) retBlock = (null, type);
            switch (block.Type) {
                case "procedures_defreturn":
                    retBlock = EmitProceduresDefReturn(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "procedures_defnoreturn":
                    retBlock = EmitProceduresDefNoReturn(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "local_declaration_expression":
                    retBlock = EmitLocalDeclarationExpression(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "controls_do_then_return":
                    retBlock = EmitControlsDoThenReturn(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "lexical_variable_get":
                    retBlock = EmitLexicalVariableGet(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "lexical_variable_set":
                    retBlock = EmitLexicalVariableSet(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "controls_while":
                    retBlock = EmitControlsWhile(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "logic_operation":
                    retBlock = EmitLogicOperation(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "logic_negate":
                    retBlock = EmitLogicNegate(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "logic_compare":
                    retBlock = EmitLogicCompare(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "math_number":
                    retBlock = EmitMathNumber(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "controls_if":
                    retBlock = EmitControlsIf(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "procedures_callreturn":
                    retBlock = EmitProceduresCallreturn(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "procedures_callnoreturn":
                    retBlock = EmitProceduresCallnoreturn(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "logic_boolean":
                    retBlock = EmitLogicBoolean(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "text":
                    retBlock = EmitText(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "math_add":
                    retBlock = EmitMathAdd(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "controls_choose":
                    retBlock = EmitControlsChoose(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                case "lists_create_with":
                    retBlock = EmitListsCreateWith(block);
                    sb.AddRange(retBlock.Item1);
                    break;
                default:
                    sb.AddRange(Todo(block.Type));
                    break;
            }

            type = retBlock.Item2;

            if (block.Nexts != null) {
                sb.AddRange(block.Nexts.SelectMany(r => {
                    var programBlock = EmitProgramBlock(r.Block);
                    type = programBlock.Item2;
                    return programBlock.Item1;
                }));
            }

            return (sb, type);
        }

        private (List<string>, string) EmitText(AiaBlock block) {
            var sb = new List<string>();
            var txt = block.Fields.Single().Value;
            sb.Add($"\"{txt}\"");
            return (sb, "string");
        }

        private string GetFromSymbolTable(string val) {
            if (_symbolTable.TryGetValue(val, out var symbolType)) {
                return symbolType;
            }

            var tmp = Guid.NewGuid().ToString();
            _symbolTable[val] = tmp;
            _symbolTableReplacements[_symbolTable[val]] = val;

            return tmp;
        }

        private IEnumerable<string> GetLocalFunctions() {
            _localFunctions.Reverse();
            var rc = _localFunctions.SelectMany(r => r).ToList();
            _localFunctions.Clear();
            return rc;
        }
    }
}
