﻿using System.Text;
using Microsoft.CodeAnalysis;

namespace SilkyUIAnalyzer;

internal static class ParseHelper
{
    public static string EscapeString(string input) => input?.Replace("\"", "\\\"").Replace("\\", "\\\\") ?? string.Empty;

    public static void ParseClassFullName(string fullName, out string ns, out string className)
    {
        var fullNameSplit = fullName.Split('.');
        ns = string.Join(".", fullNameSplit.Take(fullNameSplit.Length - 1).Select(s => s.Trim()));
        className = fullNameSplit.Last().Trim();
    }

    /// <summary>
    /// 通过属性符号和字符串值解析属性值。（右值）
    /// </summary>
    /// <param name="propSymbol"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool TryParseProperty(IPropertySymbol propSymbol, string value, out string rValue)
    {
        var code = new StringBuilder();
        switch (propSymbol.Type.SpecialType)
        {
            case SpecialType.System_String:
            {
                rValue = EscapeString(value);
                return true;
            }
            // 布尔类型
            case SpecialType.System_Boolean:
            {
                rValue = value.ToLowerInvariant();
                return true;
            }
            #region 数字类型
            case SpecialType.System_Int16:
            {
                if (short.TryParse(value, out var result))
                {
                    rValue = result.ToString();
                    return true;
                }
                break;
            }
            case SpecialType.System_Int32:
            {
                if (int.TryParse(value, out var result))
                {
                    rValue = result.ToString();
                    return true;
                }
                break;
            }
            case SpecialType.System_Double:
            {
                if (double.TryParse(value, out var result))
                {
                    rValue = $"{result}D";
                    return true;
                }
                break;
            }
            case SpecialType.System_Single:
            {
                if (float.TryParse(value, out var result))
                {
                    rValue = $"{result}F";
                    return true;
                }
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

                    if (!propSymbol.Type.GetMembers().OfType<IFieldSymbol>().Any(f => f.Name.Equals(value))) break;

                    rValue = $"{fullTypeName}.{EscapeString(value)}";
                    return true;
                }
                else if (propSymbol.Type is INamedTypeSymbol propTypeSymbol)
                {
                    return TryParseTypeProperty(propTypeSymbol, value, out rValue);
                }
                break;
            }
        }

        rValue = string.Empty;
        return false;
    }

    public static bool TryParseTypeProperty(INamedTypeSymbol propTypeSymbol, string value, out string rValue)
    {
        rValue = string.Empty;
        var displayString = propTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        switch (displayString)
        {
            case "global::Microsoft.Xna.Framework.Color":
            {
                return ParseColor(propTypeSymbol, value, out rValue);
            }
            case "global::Microsoft.Xna.Framework.Vector2":
            {
                var parts = value.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1 && float.TryParse(parts[0], out var v1))
                {
                    rValue = $"new {displayString}({v1}F)";
                    return true;
                }
                else if (parts.Length == 2 && float.TryParse(parts[0], out var v2) && float.TryParse(parts[1], out var v3))
                {
                    rValue = $"new {displayString}({v2}F, {v3}F)";
                    return true;
                }
                return false;
            }
            case "global::Microsoft.Xna.Framework.Vector3":
            {
                var parts = value.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1 && float.TryParse(parts[0], out var v1))
                {
                    rValue = $"new {displayString}({v1}F)";
                    return true;
                }
                else if (parts.Length == 3 && float.TryParse(parts[0], out var v2) && float.TryParse(parts[1], out var v3) && float.TryParse(parts[2], out var v4))
                {
                    rValue = $"new {displayString}({v2}F, {v3}F, {v4}F)";
                    return true;
                }
                return false;
            }
            case "global::Microsoft.Xna.Framework.Vector4":
            {
                var parts = value.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1 && float.TryParse(parts[0], out var v1))
                {
                    rValue = $"new {displayString}({v1}F)";
                    return true;
                }
                else if (parts.Length == 4 && float.TryParse(parts[0], out var v2) && float.TryParse(parts[1], out var v3) && float.TryParse(parts[2], out var v4) && float.TryParse(parts[3], out var v5))
                {
                    rValue = $"new {displayString}({v2}F, {v3}F, {v4}F, {v5}F)";
                    return true;
                }
                return false;
            }
            default:
            {
                if (propTypeSymbol.AllInterfaces.Any(i =>
                    i.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.IParsable<TSelf>" &&
                    SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], propTypeSymbol)))
                {
                    var fullTypeName = propTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    rValue = $"{fullTypeName}.Parse(\"{EscapeString(value)}\", null)";
                    return true;
                }
                break;
            }
        }

        return false;
    }

    // Microsoft.Xna.Framework.Color
    // Microsoft.Xna.Framework.Vector2
    // Microsoft.Xna.Framework.Vector4

    public static bool ParseColor(INamedTypeSymbol typeSymbol, string value, out string output)
    {
        output = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;

        value = value.Trim();
        var name = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (value.StartsWith("#"))
        {
            var hex = value.TrimStart('#');
            if (hex.Length != 6 && hex.Length != 8) return false;

            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            if (hex.Length == 6)
            {
                output = $"new {name}({r}, {g}, {b})";
                return true;
            }
            else
            {
                byte a = Convert.ToByte(hex.Substring(6, 2), 16);
                output = $"new {name}({r}, {g}, {b}) * {a / 255f}F";
                return true;
            }
        }
        else if (value.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) &&
            value.EndsWith(")", StringComparison.OrdinalIgnoreCase))
        {
            var parts = value.Substring(4, value.Length - 5)
                             .Split([','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) return false;

            if (byte.TryParse(parts[0], out var r) &&
                byte.TryParse(parts[1], out var g) &&
                byte.TryParse(parts[2], out var b))
            {
                output = $"new {name}({r}, {g}, {b})";
                return true;
            }
        }
        else if (value.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) &&
            value.EndsWith(")", StringComparison.OrdinalIgnoreCase))
        {
            var parts = value.Substring(5, value.Length - 6).Split([','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4) return false;

            if (byte.TryParse(parts[0], out var r) &&
                byte.TryParse(parts[1], out var g) &&
                byte.TryParse(parts[2], out var b) &&
                float.TryParse(parts[3], out var a))
            {
                output = $"new {name}({r}, {g}, {b}) * {a}F";
                return true;
            }
        }

        return false;
    }

    /// <summary> 转换为有效字段名，保留数字字母和下划线 </summary>
    public static bool IsValidMemberName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        return input.All(c => char.IsLetterOrDigit(c) || c.Equals('_'));
    }
}
