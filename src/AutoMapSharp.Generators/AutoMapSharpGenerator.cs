using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using AutoMapSharp.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapSharp.Generators;

/// <summary>
/// Incremental Roslyn source generator that emits typed mapping code for maps whose behavior it can reproduce exactly.
/// </summary>
/// <remarks>
/// <para>
/// For each <c>DefineMap&lt;TSource, TDestination&gt;()</c> call in a module, the generator checks that every option in
/// the configuration chain is one it supports, resolves every destination member with the same matching order the
/// engine uses, and mirrors the engine's choice of conversion. Anything else is left to the engine's compiled plan,
/// and an info diagnostic (AMS100) says why.
/// </para>
/// <para>
/// Each generated map also records the assumptions it was built on. The engine checks them against the frozen runtime
/// configuration before using the generated code, so a difference, for example a setting the generator could not see,
/// makes the engine fall back to its compiled plan instead of behaving differently.
/// </para>
/// </remarks>
[Generator]
public sealed class AutoMapSharpGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor NotGenerated = new(
        "AMS100",
        "Map is not generated",
        "The map from '{0}' to '{1}' uses the engine's compiled plan instead of generated code because {2}",
        "AutoMapSharp.Generation",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Generated code is only emitted for maps the generator can reproduce exactly. Other maps run on the engine's compiled expression trees, which work everywhere except Native AOT.");

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var defineMaps = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax invocation && IsDefineMapCall(invocation),
                transform: static (ctx, _) => (InvocationExpressionSyntax)ctx.Node)
            .Collect();

        context.RegisterSourceOutput(context.CompilationProvider.Combine(defineMaps), static (spc, source) =>
        {
            var (compilation, invocations) = source;
            new Emitter(spc, compilation).Run(invocations);
        });
    }

    private static bool IsDefineMapCall(InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression switch
        {
            GenericNameSyntax generic => generic,
            MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic,
            _ => null
        };

        return name != null && name.Identifier.Text == "DefineMap" && name.TypeArgumentList.Arguments.Count == 2;
    }

    private sealed class NotGeneratable : Exception
    {
        public NotGeneratable(string reason) : base(reason) { }
    }

    private sealed class Conventions
    {
        public List<string> Prefixes { get; } = new() { "Get" };
        public List<string> Suffixes { get; } = new();
        public List<string> DestinationPrefixes { get; } = new();
        public List<string> DestinationSuffixes { get; } = new();
        public List<(string, string)> Replacements { get; } = new();
        public bool SnakeCaseSource { get; set; }
    }

    private sealed class Emitter
    {
        private readonly SourceProductionContext _context;
        private readonly Compilation _compilation;
        private readonly StringBuilder _methods = new();
        private readonly StringBuilder _registrations = new();
        private int _mapIndex;

        public Emitter(SourceProductionContext context, Compilation compilation)
        {
            _context = context;
            _compilation = compilation;
        }

        public void Run(ImmutableArray<InvocationExpressionSyntax> invocations)
        {
            var mappingModule = _compilation.GetTypeByMetadataName("AutoMapSharp.MappingModule");
            if (mappingModule == null || _compilation.GetTypeByMetadataName("AutoMapSharp.Generated.GeneratedMapRegistry") == null)
            {
                return;
            }

            var maps = new List<(InvocationExpressionSyntax Invocation, INamedTypeSymbol Source, INamedTypeSymbol Destination)>();
            foreach (var invocation in invocations)
            {
                var model = _compilation.GetSemanticModel(invocation.SyntaxTree);
                if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method || method.Name != "DefineMap" || method.TypeArguments.Length != 2)
                {
                    continue;
                }

                if (method.TypeArguments[0] is not INamedTypeSymbol source || method.TypeArguments[1] is not INamedTypeSymbol destination ||
                    source.IsUnboundGenericType || destination.IsUnboundGenericType || ContainsTypeParameters(source) || ContainsTypeParameters(destination))
                {
                    continue;
                }

                maps.Add((invocation, source, destination));
            }

            var seen = new HashSet<string>();
            foreach (var (invocation, source, destination) in maps)
            {
                var key = source.ToDisplayString(TypeFormat) + "->" + destination.ToDisplayString(TypeFormat);
                if (!seen.Add(key))
                {
                    continue;
                }

                try
                {
                    GenerateMap(invocation, source, destination);
                }
                catch (NotGeneratable ex)
                {
                    _context.ReportDiagnostic(Diagnostic.Create(NotGenerated, invocation.GetLocation(), source.Name, destination.Name, ex.Message));
                }
            }

            if (_mapIndex == 0)
            {
                return;
            }

            var polyfill = _compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.ModuleInitializerAttribute") == null
                ? "namespace System.Runtime.CompilerServices\n{\n    [global::System.AttributeUsage(global::System.AttributeTargets.Method, Inherited = false)]\n    internal sealed class ModuleInitializerAttribute : global::System.Attribute { }\n}\n\n"
                : string.Empty;

            var file = new StringBuilder();
            file.AppendLine("// <auto-generated/>");
            file.AppendLine("// Generated by the AutoMapSharp source generator. The engine checks each map against the runtime configuration before using it.");
            file.AppendLine("#nullable disable");
            file.AppendLine("#pragma warning disable");
            file.AppendLine();
            file.Append(polyfill);
            file.AppendLine("namespace AutoMapSharp.Generated");
            file.AppendLine("{");
            file.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"AutoMapSharp.Generators\", \"1.0\")]");
            file.AppendLine("    internal static class AutoMapSharpGeneratedMaps");
            file.AppendLine("    {");
            file.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
            file.AppendLine("        internal static void Register()");
            file.AppendLine("        {");
            file.Append(_registrations);
            file.AppendLine("        }");
            file.Append(_methods);
            file.AppendLine("    }");
            file.AppendLine("}");

            _context.AddSource("AutoMapSharpGeneratedMaps.g.cs", SourceText.From(file.ToString(), Encoding.UTF8));
        }

        private static bool ContainsTypeParameters(ITypeSymbol type) =>
            type is ITypeParameterSymbol || type is INamedTypeSymbol named && named.TypeArguments.Any(ContainsTypeParameters) ||
            type is IArrayTypeSymbol array && ContainsTypeParameters(array.ElementType);

        // ------------------------------------------------------------------
        // One map
        // ------------------------------------------------------------------

        private sealed class MapState
        {
            public List<string> Members { get; } = new();
            public List<string> DirectPairs { get; } = new();
            public List<string> ElementPairs { get; } = new();
            public List<string> ConvertedPairs { get; } = new();
            public HashSet<string> PreservedTypes { get; } = new();
            public List<string> Stages { get; } = new() { "constructor", "BeforeMap" };
            public StringBuilder Body { get; } = new();
            public int Counter;
            public Conventions Conventions { get; set; } = new();
            public string Next(string prefix) => "__" + prefix + Counter++;
        }

        private void GenerateMap(InvocationExpressionSyntax invocation, INamedTypeSymbol source, INamedTypeSymbol destination)
        {
            RequireAccessible(source);
            RequireAccessible(destination);

            if (destination.TypeKind != TypeKind.Class || destination.IsAbstract || destination.IsStatic)
            {
                throw new NotGeneratable("the destination is not a concrete class");
            }

            if (IsEnumerable(destination) || IsStringObjectDictionary(source) || IsSimple(destination))
            {
                throw new NotGeneratable("collections, dictionaries and simple types are mapped by the engine");
            }

            if (!destination.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public))
            {
                throw new NotGeneratable("the destination has no public parameterless constructor");
            }

            var map = new MapState();
            var rules = ReadChain(invocation);
            map.Conventions = ReadConventions(invocation);

            foreach (var member in DestinationMembers(destination))
            {
                GenerateMember(map, source, member, rules);
            }

            map.Stages.Add("AfterMap");

            var index = _mapIndex++;
            var sourceName = source.ToDisplayString(TypeFormat);
            var destinationName = destination.ToDisplayString(TypeFormat);
            var stageCount = map.Stages.Count;

            foreach (var preserved in map.PreservedTypes)
            {
                _registrations.Append("            global::AutoMapSharp.Generated.GeneratedMapRegistry.Preserve<").Append(preserved).Append(">();\n");
            }

            _registrations.Append("            global::AutoMapSharp.Generated.GeneratedMapRegistry.Register(new global::AutoMapSharp.Generated.GeneratedMap<")
                .Append(sourceName).Append(", ").Append(destinationName).Append(">(Map").Append(index)
                .Append(", new string[] { ").Append(string.Join(", ", map.Members.Select(Quote))).Append(" }")
                .Append(", new global::System.Type[] { ").Append(string.Join(", ", map.DirectPairs)).Append(" }")
                .Append(", new global::System.Type[] { ").Append(string.Join(", ", map.ElementPairs)).Append(" }")
                .Append(", new global::System.Type[] { ").Append(string.Join(", ", map.ConvertedPairs.Distinct())).Append(" }));\n");

            _methods.AppendLine();
            _methods.Append("        private static readonly string[] Stages").Append(index).Append(" = { ").Append(string.Join(", ", map.Stages.Select(Quote))).AppendLine(" };");
            _methods.AppendLine();
            _methods.Append("        private static ").Append(destinationName).Append(" Map").Append(index).Append('(')
                .Append(sourceName).Append(" source, ").Append(destinationName)
                .AppendLine(" destination, bool useDestination, global::AutoMapSharp.ResolutionContext context, global::AutoMapSharp.Generated.GeneratedMapState state)");
            _methods.AppendLine("        {");
            _methods.AppendLine("            int stage = 0;");
            _methods.AppendLine("            try");
            _methods.AppendLine("            {");
            _methods.Append("                var d = useDestination && destination != null ? destination : new ").Append(destinationName).AppendLine("();");
            _methods.AppendLine("                global::AutoMapSharp.Generated.GeneratedMapHelpers.Created(context, state, source, d);");
            _methods.AppendLine("                stage = 1;");
            _methods.AppendLine("                global::AutoMapSharp.Generated.GeneratedMapHelpers.BeforeMap(context, source, d);");
            _methods.Append(map.Body);
            _methods.Append("                stage = ").Append(stageCount - 1).AppendLine(";");
            _methods.AppendLine("                global::AutoMapSharp.Generated.GeneratedMapHelpers.AfterMap(context, source, d);");
            _methods.AppendLine("                return d;");
            _methods.AppendLine("            }");
            _methods.AppendLine("            catch (global::System.Exception ex) when (!(ex is global::AutoMapSharp.Exceptions.AutoMapSharpException))");
            _methods.AppendLine("            {");
            _methods.Append("                throw global::AutoMapSharp.Generated.GeneratedMapHelpers.MemberError(ex, typeof(").Append(sourceName)
                .Append("), typeof(").Append(destinationName).Append("), Stages").Append(index).AppendLine("[stage]);");
            _methods.AppendLine("            }");
            _methods.AppendLine("        }");
        }

        private void GenerateMember(MapState map, INamedTypeSymbol source, ISymbol member, Dictionary<string, string> rules)
        {
            var memberType = MemberType(member);
            rules.TryGetValue(member.Name, out var rule);

            if (rule == "skip")
            {
                map.Members.Add(member.Name + "!skip");
                return;
            }

            if (!IsPubliclyWritable(member))
            {
                throw new NotGeneratable($"member '{member.Name}' has a non-public or init-only setter");
            }

            RequireAccessible(memberType);
            var stage = map.Stages.Count;

            if (rule == "value")
            {
                map.Members.Add(member.Name + "!value");
                map.Stages.Add(member.Name);
                map.Body.Append("                stage = ").Append(stage).AppendLine(";");
                map.Body.Append("                d.").Append(Escape(member.Name)).Append(" = state.Constant<").Append(memberType.ToDisplayString(TypeFormat))
                    .Append(">(").Append(Quote(member.Name)).AppendLine(");");
                return;
            }

            var path = FindSource(source, member.Name, map.Conventions);
            map.Members.Add(member.Name + "=>" + string.Join(".", path.Select(p => p.Name)));
            if (path.Count == 0)
            {
                return;
            }

            for (int i = 1; i < path.Count; i++)
            {
                map.PreservedTypes.Add(MemberType(path[i - 1]).ToDisplayString(TypeFormat));
            }

            var read = Read("source", path, 0, map);
            var valueType = MemberType(path[path.Count - 1]);
            var existing = !memberType.IsValueType && CanRead(member) ? "d." + Escape(member.Name) : null;
            if (!memberType.IsValueType && existing == null && member is IPropertySymbol { GetMethod: not null })
            {
                throw new NotGeneratable($"member '{member.Name}' has a non-public getter");
            }

            var value = Convert(read, valueType, memberType, existing, "context", map);
            map.Stages.Add(member.Name);
            map.Body.Append("                stage = ").Append(stage).AppendLine(";");
            map.Body.Append("                d.").Append(Escape(member.Name)).Append(" = ").Append(value).AppendLine(";");
        }

        // ------------------------------------------------------------------
        // Configuration chain and module settings
        // ------------------------------------------------------------------

        private static readonly HashSet<string> AllowedCalls = new()
        {
            "DefineMap", "ForProperty", "MaxDepth", "PreserveReferences", "AllowNullCollections", "ValidateMembers", "ForSourceMember",
            // Dispatch and inherited configuration are checked against the runtime configuration before generated code is used.
            "Include", "IncludeBase", "IncludeAllDerived"
        };

        /// <summary>
        /// Reads the calls chained on DefineMap up to ReverseMap, and returns "skip" or "value" per configured member.
        /// </summary>
        private static Dictionary<string, string> ReadChain(InvocationExpressionSyntax defineMap)
        {
            var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SyntaxNode current = defineMap;
            while (current.Parent is MemberAccessExpressionSyntax access && access.Parent is InvocationExpressionSyntax call)
            {
                var name = access.Name.Identifier.Text;
                if (name == "ReverseMap")
                {
                    break;
                }

                if (!AllowedCalls.Contains(name))
                {
                    throw new NotGeneratable($"it uses {name}");
                }

                if (name == "ForProperty")
                {
                    var (member, option) = ReadForProperty(call);
                    if (option != "ExplicitExpansion")
                    {
                        rules[member] = option == "Skip" ? "skip" : "value";
                    }
                }

                current = call;
            }

            return rules;
        }

        private static (string Member, string Option) ReadForProperty(InvocationExpressionSyntax call)
        {
            var arguments = call.ArgumentList.Arguments;
            if (arguments.Count != 2 || MemberName(arguments[0].Expression) is not { } member)
            {
                throw new NotGeneratable("a ForProperty call could not be read");
            }

            var (parameter, body) = arguments[1].Expression switch
            {
                SimpleLambdaExpressionSyntax simple => (simple.Parameter.Identifier.Text, simple.Body),
                ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } paren => (paren.ParameterList.Parameters[0].Identifier.Text, paren.Body),
                _ => (null, null)
            };

            if (body is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax target } option } &&
                target.Identifier.Text == parameter &&
                option.Name.Identifier.Text is "Skip" or "UseValue" or "ExplicitExpansion")
            {
                return (member, option.Name.Identifier.Text);
            }

            throw new NotGeneratable($"member '{member}' uses an option other than Skip or UseValue");
        }

        private static string? MemberName(ExpressionSyntax expression)
        {
            var body = expression switch
            {
                SimpleLambdaExpressionSyntax simple => simple.Body,
                ParenthesizedLambdaExpressionSyntax paren => paren.Body,
                _ => null
            };

            return body is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax } access ? access.Name.Identifier.Text : null;
        }

        private static Conventions ReadConventions(SyntaxNode node)
        {
            var conventions = new Conventions();
            var module = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (module == null) return conventions;

            foreach (var call in module.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var name = call.Expression switch
                {
                    IdentifierNameSyntax id => id.Identifier.Text,
                    MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
                    _ => null
                };

                var literals = call.ArgumentList.Arguments.Select(a => a.Expression).OfType<LiteralExpressionSyntax>()
                    .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression)).Select(l => l.Token.ValueText).ToList();

                switch (name)
                {
                    case "ClearPrefixes": conventions.Prefixes.Clear(); break;
                    case "RecognizePrefixes": conventions.Prefixes.AddRange(literals); break;
                    case "RecognizeSuffixes": conventions.Suffixes.AddRange(literals); break;
                    case "RecognizeDestinationPrefixes": conventions.DestinationPrefixes.AddRange(literals); break;
                    case "RecognizeDestinationSuffixes": conventions.DestinationSuffixes.AddRange(literals); break;
                    case "ReplaceMemberName" when literals.Count == 2: conventions.Replacements.Add((literals[0], literals[1])); break;
                    case "IncludeSourceExtensionMethods": throw new NotGeneratable("the module uses IncludeSourceExtensionMethods");
                }

                if (call.Expression is MemberAccessExpressionSyntax { Expression: var target } && target.ToString().EndsWith("ValueTransformers", StringComparison.Ordinal))
                {
                    throw new NotGeneratable("the module has value transformers");
                }
            }

            foreach (var assignment in module.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                var left = assignment.Left.ToString();
                if (left.EndsWith("ShouldMapProperty", StringComparison.Ordinal) || left.EndsWith("ShouldMapField", StringComparison.Ordinal) ||
                    left.EndsWith("ShouldMapMethod", StringComparison.Ordinal))
                {
                    throw new NotGeneratable("the module filters members with ShouldMap hooks");
                }

                if (left.EndsWith("SourceNamingConvention", StringComparison.Ordinal) && assignment.Right.ToString().Contains("SnakeCase"))
                {
                    conventions.SnakeCaseSource = true;
                }
            }

            return conventions;
        }

        // ------------------------------------------------------------------
        // Members
        // ------------------------------------------------------------------

        /// <summary>
        /// The members the engine writes: public properties with a setter of any visibility, and writable public fields.
        /// </summary>
        private static IEnumerable<ISymbol> DestinationMembers(INamedTypeSymbol type)
        {
            var names = new HashSet<string>();
            var properties = new List<ISymbol>();
            var fields = new List<ISymbol>();
            for (var current = type; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public) continue;
                    if (member is IPropertySymbol { IsIndexer: false, SetMethod: not null } property && names.Add("p:" + property.Name)) properties.Add(property);
                    if (member is IFieldSymbol { IsReadOnly: false, IsConst: false, IsImplicitlyDeclared: false } field && names.Add("f:" + field.Name)) fields.Add(field);
                }
            }

            return properties.Concat(fields);
        }

        private static List<ISymbol> SourceMembers(ITypeSymbol type)
        {
            var properties = new List<ISymbol>();
            var fields = new List<ISymbol>();
            var names = new HashSet<string>();
            for (var current = type; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public) continue;
                    if (member is IPropertySymbol { IsIndexer: false, GetMethod.DeclaredAccessibility: Accessibility.Public } property && names.Add("p:" + property.Name)) properties.Add(property);
                    if (member is IFieldSymbol { IsConst: false, IsImplicitlyDeclared: false } field && names.Add("f:" + field.Name)) fields.Add(field);
                }
            }

            return properties.Concat(fields).ToList();
        }

        /// <summary>
        /// Mirrors the engine's matching order: same name, naming convention with prefixes and suffixes, then flattening.
        /// </summary>
        private static List<ISymbol> FindSource(ITypeSymbol source, string destinationName, Conventions conventions)
        {
            var members = SourceMembers(source);
            string Replace(string name)
            {
                foreach (var (original, replacement) in conventions.Replacements) name = name.Replace(original, replacement);
                return name;
            }

            var direct = members.FirstOrDefault(m => string.Equals(Replace(m.Name), destinationName, StringComparison.OrdinalIgnoreCase));
            if (direct == null)
            {
                var destinationWords = MemberNameMatching.SplitPascal(
                    MemberNameMatching.StripPrefixesAndSuffixes(destinationName, conventions.DestinationPrefixes, conventions.DestinationSuffixes));
                direct = members.FirstOrDefault(m =>
                {
                    var stripped = MemberNameMatching.StripPrefixesAndSuffixes(Replace(m.Name), conventions.Prefixes, conventions.Suffixes);
                    var words = conventions.SnakeCaseSource ? MemberNameMatching.SplitSnake(stripped) : MemberNameMatching.SplitPascal(stripped);
                    return MemberNameMatching.WordsMatch(words, destinationWords);
                });
            }

            if (direct != null)
            {
                return new List<ISymbol> { direct };
            }

            var path = new List<ISymbol>();
            return Flatten(source, destinationName, path, conventions) ? path : new List<ISymbol>();
        }

        private static bool Flatten(ITypeSymbol type, string remaining, List<ISymbol> path, Conventions conventions)
        {
            if (remaining.Length == 0) return path.Count > 0;

            var candidates = new List<(string Name, ISymbol Member)>();
            for (var current = type; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public) continue;
                    if (member is IPropertySymbol { IsIndexer: false, GetMethod: not null } property)
                    {
                        candidates.Add((property.Name, property));
                    }
                    else if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary, Parameters.Length: 0, ReturnsVoid: false, IsGenericMethod: false } method &&
                             !OverridesObject(method) && method.Name.IndexOf('<') < 0)
                    {
                        foreach (var prefix in conventions.Prefixes)
                        {
                            if (method.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && method.Name.Length > prefix.Length)
                            {
                                candidates.Add((method.Name.Substring(prefix.Length), method));
                            }
                        }

                        candidates.Add((method.Name, method));
                    }
                }
            }

            foreach (var (name, member) in candidates.Where(c => remaining.StartsWith(c.Name, StringComparison.OrdinalIgnoreCase)).OrderByDescending(c => c.Name.Length))
            {
                path.Add(member);
                var rest = remaining.Substring(name.Length);
                if (rest.Length == 0 || Flatten(MemberType(member), rest, path, conventions))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
            }

            return false;
        }

        private static bool OverridesObject(IMethodSymbol method)
        {
            for (var current = method; current != null; current = current.OverriddenMethod)
            {
                if (current.ContainingType.SpecialType == SpecialType.System_Object) return true;
            }

            return false;
        }

        /// <summary>
        /// A read of a member path. A null intermediate object gives the default value, as in the engine.
        /// </summary>
        private string Read(string instance, List<ISymbol> path, int index, MapState map)
        {
            var access = instance + "." + Escape(path[index].Name) + (path[index] is IMethodSymbol ? "()" : string.Empty);
            if (index == path.Count - 1)
            {
                return access;
            }

            var type = MemberType(path[index]);
            if (type.IsValueType)
            {
                return Read("(" + access + ")", path, index + 1, map);
            }

            var variable = map.Next("p");
            var finalType = MemberType(path[path.Count - 1]).ToDisplayString(TypeFormat);
            return "(" + access + " is { } " + variable + " ? " + Read(variable, path, index + 1, map) + " : default(" + finalType + "))";
        }

        // ------------------------------------------------------------------
        // Conversions, mirroring the engine's PlanBuilder.ConvertValue
        // ------------------------------------------------------------------

        private string Convert(string value, ITypeSymbol from, ITypeSymbol to, string? existing, string context, MapState map)
        {
            var fromName = from.ToDisplayString(TypeFormat);
            var toName = to.ToDisplayString(TypeFormat);
            var pair = "typeof(" + fromName + "), typeof(" + toName + ")";

            if (IsEnumerable(to) && IsEnumerable(from))
            {
                return ConvertCollection(value, from, to, existing, context, map);
            }

            // Same type: assigned. The engine would map it instead only when this engine defines a map for the pair,
            // which the runtime check detects.
            if (SymbolEqualityComparer.Default.Equals(from, to))
            {
                if (!IsSimple(to)) map.DirectPairs.Add(pair);
                return value;
            }

            if (IsAssignable(from, to))
            {
                map.ConvertedPairs.Add(pair);
                return "(" + toName + ")" + Parenthesize(value);
            }

            if (UnderlyingNullable(from) is { } underlyingFrom)
            {
                map.ConvertedPairs.Add(pair);
                var variable = map.Next("n");
                return "(" + Parenthesize(value) + " is { } " + variable + " ? " + Convert(variable, underlyingFrom, to, existing, context, map) + " : default(" + toName + "))";
            }

            if (UnderlyingNullable(to) is { } underlyingTo)
            {
                map.ConvertedPairs.Add(pair);
                if (from.IsValueType)
                {
                    return "(" + toName + ")" + Parenthesize(Convert(value, from, underlyingTo, null, context, map));
                }

                var variable = map.Next("r");
                return "(" + Parenthesize(value) + " is { } " + variable + " ? (" + toName + ")" + Parenthesize(Convert(variable, from, underlyingTo, null, context, map)) + " : default(" + toName + "))";
            }

            if (from.IsValueType)
            {
                return ConvertNonNull(value, from, to, existing, context, map);
            }

            var guard = map.Next("g");
            return "(" + Parenthesize(value) + " is { } " + guard + " ? " + ConvertNonNull(guard, from, to, existing, context, map) + " : default(" + toName + "))";
        }

        private string ConvertNonNull(string value, ITypeSymbol from, ITypeSymbol to, string? existing, string context, MapState map)
        {
            if (HasConversionOperator(from, to))
            {
                throw new NotGeneratable($"{from.Name} to {to.Name} uses a conversion operator");
            }

            var builtIn = BuiltIn(value, from, to);
            if (builtIn != null)
            {
                map.ConvertedPairs.Add("typeof(" + from.ToDisplayString(TypeFormat) + "), typeof(" + to.ToDisplayString(TypeFormat) + ")");
                return builtIn;
            }

            if (!IsSimple(to) && to.SpecialType != SpecialType.System_Object)
            {
                return Nested(value, from, to, existing, context);
            }

            throw new NotGeneratable($"{from.Name} to {to.Name} needs a run-time conversion");
        }

        private string Nested(string value, ITypeSymbol from, ITypeSymbol to, string? existing, string context)
        {
            RequireAccessible(from);
            RequireAccessible(to);
            var toName = to.ToDisplayString(TypeFormat);
            return "global::AutoMapSharp.Generated.GeneratedMapHelpers.MapNested<" + from.ToDisplayString(TypeFormat) + ", " + toName + ">(" + context + ", " + value + ", " +
                   (existing ?? "default(" + toName + ")") + ", " + (existing != null ? existing + " != null" : "false") + ")";
        }

        private string ConvertCollection(string value, ITypeSymbol from, ITypeSymbol to, string? existing, string context, MapState map)
        {
            if (IsDictionary(to) || IsDictionary(from))
            {
                throw new NotGeneratable("dictionary members are mapped by the engine");
            }

            var fromElement = ElementType(from) ?? throw new NotGeneratable($"the element type of {from.Name} is not known");
            var toElement = ElementType(to) ?? throw new NotGeneratable($"the element type of {to.Name} is not known");
            RequireAccessible(fromElement);
            RequireAccessible(toElement);

            var fromElementName = fromElement.ToDisplayString(TypeFormat);
            var toElementName = toElement.ToDisplayString(TypeFormat);
            map.ElementPairs.Add("typeof(" + fromElementName + "), typeof(" + toElementName + ")");

            var item = map.Next("i");
            var itemContext = map.Next("c");
            var element = Convert(item, fromElement, toElement, null, itemContext, map);
            var converter = "(" + fromElementName + " " + item + ", global::AutoMapSharp.ResolutionContext " + itemContext + ") => " + element;
            var items = "(global::System.Collections.Generic.IEnumerable<" + fromElementName + ">)" + Parenthesize(value);

            if (to is IArrayTypeSymbol)
            {
                return "global::AutoMapSharp.Generated.GeneratedMapHelpers.MapToArray<" + fromElementName + ", " + toElementName + ">(" + context + ", state, " + items + ", " + converter + ")";
            }

            var toName = to.ToDisplayString(TypeFormat);
            var listOk = to is INamedTypeSymbol named && named.IsGenericType &&
                         named.ConstructedFrom.ToDisplayString() is "System.Collections.Generic.List<T>" or "System.Collections.Generic.IList<T>" or
                             "System.Collections.Generic.ICollection<T>" or "System.Collections.Generic.IEnumerable<T>" or
                             "System.Collections.Generic.IReadOnlyList<T>" or "System.Collections.Generic.IReadOnlyCollection<T>";
            if (!listOk)
            {
                throw new NotGeneratable($"collection type {to.Name} is mapped by the engine");
            }

            return "global::AutoMapSharp.Generated.GeneratedMapHelpers.MapToList<" + toName + ", " + fromElementName + ", " + toElementName + ">(" + context + ", state, " + items + ", " +
                   (existing ?? "null") + ", " + converter + ")";
        }

        /// <summary>
        /// Mirrors BuiltInConversions.TryBuild in the engine. Returns null when the engine would not use a built-in conversion.
        /// </summary>
        private string? BuiltIn(string value, ITypeSymbol from, ITypeSymbol to)
        {
            var toName = to.ToDisplayString(TypeFormat);
            var fromName = from.ToDisplayString(TypeFormat);
            const string invariant = "global::System.Globalization.CultureInfo.InvariantCulture";

            if (to.SpecialType == SpecialType.System_String)
            {
                if (fromName is "global::System.DateOnly" or "global::System.TimeOnly") return Parenthesize(value) + ".ToString(\"O\", " + invariant + ")";
                if (from.TypeKind != TypeKind.Enum && FindConvertMethod("ToString", from, withProvider: true)) return "global::System.Convert.ToString(" + value + ", " + invariant + ")";
                return Parenthesize(value) + ".ToString()";
            }

            if (to.TypeKind == TypeKind.Enum)
            {
                if (from.SpecialType == SpecialType.System_String) return "global::AutoMapSharp.Generated.GeneratedMapHelpers.ParseEnum<" + toName + ">(" + value + ")";
                if (from.TypeKind == TypeKind.Enum) return "global::AutoMapSharp.Generated.GeneratedMapHelpers.MapEnumByName<" + fromName + ", " + toName + ">(" + value + ")";
                if (IsNumeric(from)) return "(" + toName + ")" + Parenthesize(NumericConvert(value, from, ((INamedTypeSymbol)to).EnumUnderlyingType!));
                return null;
            }

            if (from.TypeKind == TypeKind.Enum)
            {
                var underlying = ((INamedTypeSymbol)from).EnumUnderlyingType!;
                return IsNumeric(to) ? NumericConvert("(" + underlying.ToDisplayString(TypeFormat) + ")" + Parenthesize(value), underlying, to) : null;
            }

            if (toName == "global::System.Guid" && from.SpecialType == SpecialType.System_String) return "global::System.Guid.Parse(" + value + ")";
            if (toName == "global::System.DateTimeOffset" && from.SpecialType == SpecialType.System_DateTime) return "new global::System.DateTimeOffset(" + value + ")";
            if (to.SpecialType == SpecialType.System_DateTime && fromName == "global::System.DateTimeOffset") return Parenthesize(value) + ".DateTime";

            if (toName == "global::System.DateOnly" || toName == "global::System.TimeOnly")
            {
                if (from.SpecialType == SpecialType.System_DateTime) return toName + ".FromDateTime(" + value + ")";
                if (fromName == "global::System.DateTimeOffset") return toName + ".FromDateTime(" + Parenthesize(value) + ".DateTime)";
                if (from.SpecialType == SpecialType.System_String) return toName + ".Parse(" + value + ", " + invariant + ")";
            }

            if (fromName == "global::System.DateOnly" && to.SpecialType == SpecialType.System_DateTime)
            {
                return Parenthesize(value) + ".ToDateTime(global::System.TimeOnly.MinValue)";
            }

            var convertName = ConvertTarget(to);
            if (convertName != null && ImplementsIConvertible(from) && !AlwaysThrows(from, to))
            {
                if (FindConvertMethod(convertName, from, withProvider: true)) return "global::System.Convert." + convertName + "(" + value + ", " + invariant + ")";
                if (FindConvertMethod(convertName, from, withProvider: false)) return "global::System.Convert." + convertName + "(" + value + ")";
            }

            if (IsSimple(to))
            {
                throw new NotGeneratable($"{from.Name} to {to.Name} may use a TypeConverter at run time");
            }

            return null;
        }

        private string NumericConvert(string value, ITypeSymbol from, ITypeSymbol to)
        {
            if (SymbolEqualityComparer.Default.Equals(from, to)) return value;
            var name = ConvertTarget(to);
            if (name != null && FindConvertMethod(name, from, withProvider: false)) return "global::System.Convert." + name + "(" + value + ")";
            return "checked((" + to.ToDisplayString(TypeFormat) + ")" + Parenthesize(value) + ")";
        }

        private bool FindConvertMethod(string name, ITypeSymbol parameterType, bool withProvider)
        {
            var convert = _compilation.GetTypeByMetadataName("System.Convert");
            var provider = _compilation.GetTypeByMetadataName("System.IFormatProvider");
            return convert != null && convert.GetMembers(name).OfType<IMethodSymbol>().Any(m =>
                m.Parameters.Length == (withProvider ? 2 : 1) &&
                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, parameterType) &&
                (!withProvider || SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, provider)));
        }

        private static string? ConvertTarget(ITypeSymbol type) => type.SpecialType switch
        {
            SpecialType.System_Boolean => "ToBoolean",
            SpecialType.System_Char => "ToChar",
            SpecialType.System_SByte => "ToSByte",
            SpecialType.System_Byte => "ToByte",
            SpecialType.System_Int16 => "ToInt16",
            SpecialType.System_UInt16 => "ToUInt16",
            SpecialType.System_Int32 => "ToInt32",
            SpecialType.System_UInt32 => "ToUInt32",
            SpecialType.System_Int64 => "ToInt64",
            SpecialType.System_UInt64 => "ToUInt64",
            SpecialType.System_Single => "ToSingle",
            SpecialType.System_Double => "ToDouble",
            SpecialType.System_Decimal => "ToDecimal",
            SpecialType.System_DateTime => "ToDateTime",
            _ => null
        };

        private static bool AlwaysThrows(ITypeSymbol from, ITypeSymbol to)
        {
            var f = from.SpecialType;
            var t = to.SpecialType;
            if (f == t || f == SpecialType.System_String || t == SpecialType.System_String) return false;
            if (f == SpecialType.System_DateTime || t == SpecialType.System_DateTime) return true;
            var floating = new[] { SpecialType.System_Boolean, SpecialType.System_Single, SpecialType.System_Double, SpecialType.System_Decimal };
            if (f == SpecialType.System_Char) return floating.Contains(t);
            if (t == SpecialType.System_Char) return floating.Contains(f);
            return false;
        }

        private static bool IsNumeric(ITypeSymbol type) => type.TypeKind != TypeKind.Enum && type.SpecialType is
            SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64 or
            SpecialType.System_Int16 or SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Decimal or SpecialType.System_Double or SpecialType.System_Single;

        private static bool ImplementsIConvertible(ITypeSymbol type) => type.AllInterfaces.Any(i => i.ToDisplayString() == "System.IConvertible");

        private bool IsAssignable(ITypeSymbol from, ITypeSymbol to)
        {
            var conversion = _compilation.ClassifyConversion(from, to);
            return conversion.IsImplicit && !conversion.IsUserDefined && !conversion.IsNumeric &&
                   (conversion.IsIdentity || conversion.IsReference || conversion.IsBoxing || conversion.IsNullable && SymbolEqualityComparer.Default.Equals(UnderlyingNullable(to), from));
        }

        private static bool HasConversionOperator(ITypeSymbol from, ITypeSymbol to) =>
            new[] { from, to }.SelectMany(t => t.GetMembers().OfType<IMethodSymbol>())
                .Any(m => m.MethodKind == MethodKind.Conversion &&
                          SymbolEqualityComparer.Default.Equals(m.ReturnType, to) &&
                          m.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, from));

        // ------------------------------------------------------------------
        // Type helpers
        // ------------------------------------------------------------------

        private static ITypeSymbol MemberType(ISymbol member) => member switch
        {
            IPropertySymbol p => p.Type,
            IFieldSymbol f => f.Type,
            IMethodSymbol m => m.ReturnType,
            _ => throw new NotGeneratable($"member '{member.Name}' cannot be read")
        };

        private static bool IsPubliclyWritable(ISymbol member) => member switch
        {
            IPropertySymbol { SetMethod: { DeclaredAccessibility: Accessibility.Public, IsInitOnly: false } } => true,
            IFieldSymbol { IsReadOnly: false } => true,
            _ => false
        };

        private static bool CanRead(ISymbol member) => member switch
        {
            IPropertySymbol { GetMethod.DeclaredAccessibility: Accessibility.Public } => true,
            IFieldSymbol => true,
            _ => false
        };

        private static ITypeSymbol? UnderlyingNullable(ITypeSymbol type) =>
            type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named ? named.TypeArguments[0] : null;

        private static bool IsSimple(ITypeSymbol type)
        {
            type = UnderlyingNullable(type) ?? type;
            return type.TypeKind == TypeKind.Enum || type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Collections_IEnumerable ||
                   type.ContainingNamespace?.ToDisplayString() == "System";
        }

        private static bool IsEnumerable(ITypeSymbol type) =>
            type.SpecialType != SpecialType.System_String &&
            (type is IArrayTypeSymbol || type.AllInterfaces.Any(i => i.ToDisplayString() == "System.Collections.IEnumerable") || type.ToDisplayString() == "System.Collections.IEnumerable");

        private static bool IsDictionary(ITypeSymbol type) =>
            type.AllInterfaces.Concat(type is INamedTypeSymbol n && n.TypeKind == TypeKind.Interface ? new[] { n } : Array.Empty<INamedTypeSymbol>())
                .Any(i => i.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.IDictionary<TKey, TValue>" or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>");

        private static bool IsStringObjectDictionary(ITypeSymbol type) =>
            type.AllInterfaces.Any(i => i.Name is "IDictionary" or "IReadOnlyDictionary" && i.TypeArguments.Length == 2 &&
                                        i.TypeArguments[0].SpecialType == SpecialType.System_String && i.TypeArguments[1].SpecialType == SpecialType.System_Object);

        private static ITypeSymbol? ElementType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array) return array.ElementType;
            if (type is INamedTypeSymbol { IsGenericType: true } named && named.ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return named.TypeArguments[0];
            }

            return type.AllInterfaces.FirstOrDefault(i => i.ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)?.TypeArguments[0];
        }

        private void RequireAccessible(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                RequireAccessible(array.ElementType);
                return;
            }

            if (!_compilation.IsSymbolAccessibleWithin(type, _compilation.Assembly))
            {
                throw new NotGeneratable($"type {type.Name} is not accessible to generated code");
            }

            if (type is INamedTypeSymbol named)
            {
                foreach (var argument in named.TypeArguments) RequireAccessible(argument);
            }
        }

        private static string Parenthesize(string value) => "(" + value + ")";

        private static string Escape(string name) => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

        private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
