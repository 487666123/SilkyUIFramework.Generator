using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using static Microsoft.CodeAnalysis.CSharp.SyntaxTokenParser;

namespace SilkyUIAnalyzer;

public static class ParseHelper
{
    public static void ParseClassFullName(string fullName, out string ns, out string className)
    {
        var fullNameSplit = fullName.Split('.');
        ns = string.Join(".", fullNameSplit.Take(fullNameSplit.Length - 1).Select(s => s.Trim()));
        className = fullNameSplit.Last().Trim();
    }

    public static string P(IPropertySymbol propSymbol, string value)
    {
        var code = new StringBuilder();
        switch (propSymbol.Type.SpecialType)
        {
            case SpecialType.System_String:
            {
                return EscapeString(value);
                break;
            }
            // 布尔类型
            case SpecialType.System_Boolean:
            {
                value.ToLowerInvariant();
                break;
            }
            #region 数字类型
            case SpecialType.System_Int16:
            {
                if (short.TryParse(value, out var result))
                    return result.ToString();
                break;
            }
            case SpecialType.System_Int32:
            {
                if (int.TryParse(value, out var result))
                    return result.ToString();
                break;
            }
            case SpecialType.System_Double:
            {
                if (double.TryParse(value, out var result))
                    return result.ToString();
                break;
            }
            case SpecialType.System_Single:
            {
                if (float.TryParse(value, out var result))
                    return result.ToString();
                break;
            }
            #endregion
            case SpecialType.None:
            {
                // 特殊类型为 None 时，可能是 enum 或自定义类型
                if (propSymbol.Type.TypeKind == TypeKind.Enum)
                {
                    // 枚举类型
                    var fullTypeName = propSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var enumMemberNames = propSymbol.Type.GetMembers()
                        .OfType<IFieldSymbol>().Select(f => f.Name).ToArray();

                    if (!enumMemberNames.Contains(value)) break;

                    return EscapeString(value);
                }
                else if (propSymbol.Type is INamedTypeSymbol propTypeSymbol)
                {
                    // 判断是否实现 System.IParsable<T>

                    if (propTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Microsoft.Xna.Framework.Color")
                    {
                        return ParseColor(propTypeSymbol, value);
                    }
                    else if (propTypeSymbol.AllInterfaces.Any(i =>
                        i.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.IParsable<TSelf>" &&
                        SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], propTypeSymbol)))
                    {
                        var fullTypeName = propTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        return EscapeString(value);
                    }
                }
                break;
            }
        }
    }

    // Microsoft.Xna.Framework.Color
    // Microsoft.Xna.Framework.Vector2
    // Microsoft.Xna.Framework.Vector4

    public static string ParseColor(INamedTypeSymbol typeSymbol, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        value = value.Trim();
        var name = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (value.StartsWith("#"))
        {
            var hex = value.TrimStart('#');
            if (hex.Length != 6 && hex.Length != 8) return string.Empty;

            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            if (hex.Length == 6)
            {
                return $"new {name}({r}, {g}, {b})";
            }
            else
            {
                byte a = Convert.ToByte(hex.Substring(6, 2), 16);
                return $"new {name}({r}, {g}, {b}) * {a / 255f}F";
            }
        }
        else if (value.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) &&
            value.EndsWith(")", StringComparison.OrdinalIgnoreCase))
        {
            var parts = value.Substring(4, value.Length - 5)
                             .Split([','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) return string.Empty;

            if (byte.TryParse(parts[0], out var r) &&
                byte.TryParse(parts[1], out var g) &&
                byte.TryParse(parts[2], out var b))
            {
                return $"new {name}({r}, {g}, {b})";
            }
        }
        else if (value.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) &&
            value.EndsWith(")", StringComparison.OrdinalIgnoreCase))
        {
            var parts = value.Substring(5, value.Length - 6).Split([','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4) return string.Empty;

            if (byte.TryParse(parts[0], out var r) &&
                byte.TryParse(parts[1], out var g) &&
                byte.TryParse(parts[2], out var b) &&
                float.TryParse(parts[3], out var a))
            {
                return $"new {name}({r}, {g}, {b}) * {a}F";
            }
        }

        return string.Empty;
    }
}
