using System.Globalization;

namespace CopperCalzone.Parsing;

public sealed class SExpNode
{
    public string? Atom { get; }
    public int Count => List?.Count ?? 0;
    public bool IsList => List is not null;
    public List<SExpNode>? List { get; }

    public string Tag => IsList && List!.Count > 0 && !List[0].IsList
        ? (List[0].Atom ?? "")
        : "";

    public SExpNode(string atom)
    {
        Atom = atom;
    }

    public SExpNode(List<SExpNode> list)
    {
        List = list;
    }

    public SExpNode? this[int index] => List is not null && index >= 0 && index < List.Count
        ? List[index]
        : null;

    public List<SExpNode> FindAllSub(string tag)
    {
        var res = new List<SExpNode>();
        if (List is null) return res;
        for (int i = 0; i < List.Count; i++)
        {
            var item = List[i];
            if (item.IsList && item.Tag == tag)
                res.Add(item);
        }
        return res;
    }

    public SExpNode? FindSub(string tag)
    {
        if (List is null) return null;
        for (int i = 0; i < List.Count; i++)
        {
            var item = List[i];
            if (item.IsList && item.Tag == tag)
                return item;
        }
        return null;
    }

    public double GetDouble(int index, double defaultValue = 0.0)
    {
        var child = this[index];
        if (child is { IsList: false, Atom: not null } &&
            double.TryParse(child.Atom, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
        {
            return val;
        }
        return defaultValue;
    }

    public int? GetInt(int index)
    {
        var child = this[index];
        if (child is { IsList: false, Atom: not null } &&
            int.TryParse(child.Atom, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val))
        {
            return val;
        }
        return null;
    }

    public string GetString(int index, string defaultValue = "")
    {
        var child = this[index];
        return child is { IsList: false, Atom: not null } ? child.Atom : defaultValue;
    }
}