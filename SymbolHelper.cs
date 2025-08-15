using Microsoft.CodeAnalysis;

namespace SilkyUIAnalyzer;

public static class SymbolHelper
{
    /// <summary>
    /// 获取指定类型的所有成员，包括继承的成员，但是如若在本类型重写，继承的将不会获取。
    /// </summary>
    /// <param name="typeSymbol"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static List<ISymbol> GetOnlyMembers(this INamedTypeSymbol typeSymbol, string name)
    {
        var members = new List<ISymbol>();

        while (typeSymbol != null)
        {
            foreach (var item in typeSymbol.GetMembers(name))
            {
                if (members.Any(s => s.Name == item.Name)) continue;
                members.Add(item);
            }

            typeSymbol = typeSymbol.BaseType;
        }

        return members;
    }
}
