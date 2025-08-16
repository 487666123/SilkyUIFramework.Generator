using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace SilkyUIAnalyzer;

internal class ComponentGeneratorLogic
{
    private static int _variableCounter;
    private static HashSet<string> ValidMemberName = [];
    public static Dictionary<string, INamedTypeSymbol> AliasToTypeSymbolMapping { get; set; }

    /// <summary> 特殊属性，不需要生成字段或赋值语句的属性名列表。 </summary>
    public static HashSet<string> SpecialAttributes { get; } = ["Name", "Class"];

    public static string GenerateCode(XElement root, INamedTypeSymbol typeSymbol)
    {
        try
        {
            ValidMemberName.Clear();
            _variableCounter = 0;

            var ns = typeSymbol.ContainingNamespace.ToDisplayString();
            var className = typeSymbol.Name;
            var accessibility = typeSymbol.DeclaredAccessibility.ToString().ToLowerInvariant();

            var code = new StringBuilder();

            // 首先生成类定义和字段声明
            code.AppendLine($$"""
            namespace {{ns}}
            {
                {{accessibility}} partial class {{className}}
                {
            {{GeneratePropertyDeclarations(root, 8)}}

                    private bool _contentLoaded;

                    public void InitializeComponent()
                    {
                        if (_contentLoaded) return;
                        _contentLoaded = true;

            {{GenerateAssignments(typeSymbol, root.Attributes(), "this", 12)}}
            """);

            if (root.HasElements)
            {
                var indent = new string(' ', 12);
                foreach (var item in root.Elements())
                {
                    if (!AliasToTypeSymbolMapping.TryGetValue(item.Name.LocalName, out var itemTypeSymbol)) continue;

                    var (elementCode, variableName) = GenerateElement(itemTypeSymbol, item, 12);
                    code.Append(elementCode);

                    // 映射
                    if (item.Attribute("Name") is { } nameAttr && ValidMemberName.Contains(nameAttr.Value))
                    {
                        code.AppendLine($"{indent}{nameAttr.Value} = {variableName};");
                    }

                    // 添加到类中
                    code.AppendLine($"{indent}Add({variableName});");
                }
            }

            return code.AppendLine($$"""
                    }
                }
            }
            """).ToString();
        }
        catch (XmlException)
        {
            return string.Empty;
        }
    }

    /// <summary> 收集并生成属性声明 </summary>
    private static string GeneratePropertyDeclarations(XElement element, int indentLevel)
    {
        var indent = new string(' ', indentLevel);
        var code = new StringBuilder();

        foreach (var child in element.Elements())
        {
            if (child.Attribute("Name") is { } nameAttr &&
                IsValidMemberName(nameAttr.Value) && ValidMemberName.Add(nameAttr.Value))
            {
                var typeSymbol = AliasToTypeSymbolMapping[child.Name.LocalName];
                var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                code.AppendLine($$"""{{indent}}public {{typeName}} {{nameAttr.Value}} { get; private set; }""");
            }

            if (child.HasElements)
            {
                code.Append(GeneratePropertyDeclarations(child, indentLevel));
            }
        }

        return code.ToString();
    }


    /// <summary> 生成属性赋值语句 </summary>
    private static string GenerateAssignments(INamedTypeSymbol typeSymbol, IEnumerable<XAttribute> xAttributes, string target, int indentLevel)
    {
        var indent = new string(' ', indentLevel);
        var code = new StringBuilder();

        //code.AppendLine($"// {typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");
        //code.AppendLine($"// {string.Join(", ", typeSymbol.GetMembers().Select(m => m.Name))}");

        foreach (var attribute in xAttributes)
        {
            // 属性名
            var propertyName = attribute.Name.LocalName;

            // 跳过 Name 属性，因为它已经用于创建字段
            if (SpecialAttributes.Contains(propertyName)) continue;

            var value = attribute.Value;

            //code.AppendLine($"// {propertyName} - {parent.GetMembers(propertyName).Length} - {string.Join(", ", [.. parent.GetMembers(propertyName).Select(m => m.Name)])} - 10");
            // code.AppendLine($$"""{{string.Join(", ", GetAllMembers(typeSymbol, propertyName))}}""");

            if (typeSymbol.GetOnlyMembers(propertyName) is not { } memberSymbols ||
                memberSymbols.Count != 1 ||
                memberSymbols.First() is not IPropertySymbol propSymbol ||
                propSymbol.SetMethod == null) continue;

            if (ParseHelper.TryParseProperty(propSymbol, value, out var rValue))
            {
                code.AppendLine($"{indent}{target}.{propertyName} = {rValue};");
            }

            //switch (propSymbol.Type.SpecialType)
            //{
            //    // 字符串类型
            //    case SpecialType.System_String:
            //    {
            //        code.AppendLine($"{indent}{target}.{propertyName} = \"{ParseHelper.EscapeString(value)}\";");
            //        break;
            //    }
            //    // 布尔类型
            //    case SpecialType.System_Boolean:
            //    {
            //        code.AppendLine($"{indent}{target}.{propertyName} = {value.ToLowerInvariant()};");
            //        break;
            //    }
            //    #region 数字类型
            //    case SpecialType.System_Int16:
            //    {
            //        if (short.TryParse(value, out var result))
            //            code.AppendLine($"{indent}{target}.{propertyName} = {result};");
            //        break;
            //    }
            //    case SpecialType.System_Int32:
            //    {
            //        if (int.TryParse(value, out var result))
            //            code.AppendLine($"{indent}{target}.{propertyName} = {result};");
            //        break;
            //    }
            //    case SpecialType.System_Double:
            //    {
            //        if (double.TryParse(value, out var result))
            //            code.AppendLine($"{indent}{target}.{propertyName} = {result}D;");
            //        break;
            //    }
            //    case SpecialType.System_Single:
            //    {
            //        if (float.TryParse(value, out var result))
            //            code.AppendLine($"{indent}{target}.{propertyName} = {result}F;");
            //        break;
            //    }
            //    #endregion
            //    case SpecialType.None:
            //    {
            //        // 特殊类型为 None 时，可能是 enum 或自定义类型
            //        if (propSymbol.Type.TypeKind == TypeKind.Enum)
            //        {
            //            // 枚举类型
            //            var fullTypeName = propSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            //            var enumMemberNames = propSymbol.Type.GetMembers()
            //                .OfType<IFieldSymbol>().Select(f => f.Name).ToArray();

            //            if (!enumMemberNames.Contains(value)) break;

            //            code.AppendLine($"{indent}{target}.{propertyName} = {fullTypeName}.{ParseHelper.EscapeString(value)};");
            //        }
            //        else if (propSymbol.Type is INamedTypeSymbol propTypeSymbol)
            //        {
            //            // 判断是否实现 System.IParsable<T>

            //            if (propTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Microsoft.Xna.Framework.Color")
            //            {
            //                //code.AppendLine($"{indent}{target}.{propertyName} = {ParseHelper.ParseColor(propTypeSymbol, value)};");
            //            }
            //            else if (propTypeSymbol.AllInterfaces.Any(i =>
            //                i.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.IParsable<TSelf>" &&
            //                SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], propTypeSymbol)))
            //            {
            //                var fullTypeName = propTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            //                code.AppendLine($"{indent}{target}.{propertyName} = {fullTypeName}.Parse(\"{ParseHelper.EscapeString(value)}\", null);");
            //            }
            //        }
            //        break;
            //    }
            //}
        }

        return code.ToString();
    }

    private static (string code, string variableName) GenerateElement(INamedTypeSymbol typeSymbol, XElement element, int indentLevel)
    {
        var indent = new string(' ', indentLevel);
        var code = new StringBuilder();

        var uniqueVariableName = $"element{++_variableCounter}";

        code.AppendLine($"{indent}var {uniqueVariableName} = new global::{typeSymbol}();");

        // 属性赋值
        code.Append(GenerateAssignments(typeSymbol, element.Attributes(), uniqueVariableName, indentLevel));

        if (element.HasElements)
        {
            foreach (var item in element.Elements())
            {
                if (!AliasToTypeSymbolMapping.TryGetValue(item.Name.LocalName, out var itemTypeSymbol)) continue;

                var (childCode, childVariableName) = GenerateElement(itemTypeSymbol, item, indentLevel);
                code.Append(childCode);

                if (item.Attribute("Name") is { } nameAttr && ValidMemberName.Contains(nameAttr.Value))
                {
                    code.AppendLine($"{indent}{nameAttr.Value} = {childVariableName};");
                }
                code.AppendLine($"{indent}{uniqueVariableName}.Add({childVariableName});");
            }
        }

        return (code.ToString(), uniqueVariableName);
    }

    /// <summary>
    /// 转换为有效字段名，保留数字字母和下划线
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private static bool IsValidMemberName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        return input.All(c => char.IsLetterOrDigit(c) || c.Equals('_'));
    }
}