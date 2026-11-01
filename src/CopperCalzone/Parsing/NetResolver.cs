namespace CopperCalzone.Parsing;

public sealed class NetResolver
{
    private readonly Dictionary<int, string> _byId = [];
    private readonly Dictionary<string, int> _byName = new(StringComparer.Ordinal);
    private int _nextId = 1;

    public List<(int Id, string Name)> AllNets()
    {
        var result = new List<(int, string)>(_byName.Count);
        foreach (var kvp in _byName)
        {
            result.Add((kvp.Value, kvp.Key));
        }
        return result;
    }

    public (int Id, string Name) Register(int? netId = null, string netName = "")
    {
        netName = netName?.Trim() ?? "";
        if (string.IsNullOrEmpty(netName))
            return (0, "");

        if (_byName.TryGetValue(netName, out int assignedId))
        {
            if (netId.HasValue)
                _byId[netId.Value] = netName;
            return (assignedId, netName);
        }

        if (netId.HasValue && netId.Value > 0)
        {
            assignedId = netId.Value;
        }
        else
        {
            while (_byId.ContainsKey(_nextId))
                _nextId++;
            assignedId = _nextId;
            _nextId++;
        }

        _byId[assignedId] = netName;
        _byName[netName] = assignedId;
        return (assignedId, netName);
    }

    public (int Id, string Name) Resolve(SExpNode? netNode)
    {
        if (netNode is null || netNode.Count < 2)
            return (0, "");

        int? idVal = netNode.GetInt(1);
        string val2 = netNode.Count > 2 ? netNode.GetString(2) : "";

        if (idVal.HasValue)
        {
            if (!string.IsNullOrEmpty(val2))
            {
                return Register(idVal.Value, val2);
            }
            else
            {
                if (!_byId.TryGetValue(idVal.Value, out string? name) || string.IsNullOrEmpty(name))
                {
                    if (idVal.Value > 0)
                    {
                        name = $"Net_{idVal.Value}";
                        _byId[idVal.Value] = name;
                        _byName[name] = idVal.Value;
                    }
                    else
                    {
                        name = "";
                    }
                }
                return (idVal.Value, name);
            }
        }
        else
        {
            string name = netNode.GetString(1).Trim();
            return Register(null, name);
        }
    }
}