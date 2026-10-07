namespace LexForge244;

/// <summary>由 NFA 经含 epsilon 闭包的子集构造得到的共享 DFA。</summary>
internal sealed class Dfa
{
    public const int MaxStates = 4096;

    // 每状态: 按区间排序的 (lo, hi, target)；AcceptRule < 0 表示非接受。
    private readonly List<List<(char Lo, char Hi, int To)>> _trans = [];
    private readonly List<int> _accept = [];

    public int StateCount => _trans.Count;

    public static Dfa Build(Nfa nfa)
    {
        var dfa = new Dfa();
        var ids = new Dictionary<string, int>();
        var queue = new Queue<HashSet<Nfa.State>>();

        var startSet = EpsilonClosure([nfa.Start]);
        ids[Key(startSet)] = 0;
        dfa.AddState(startSet);
        queue.Enqueue(startSet);

        while (queue.Count > 0)
        {
            var set = queue.Dequeue();
            int from = ids[Key(set)];
            foreach (var (lo, hi, targets) in CollectMoves(set))
            {
                var next = EpsilonClosure(targets);
                var key = Key(next);
                if (!ids.TryGetValue(key, out int to))
                {
                    if (dfa.StateCount >= MaxStates)
                        throw new LexException($"DFA 状态数超过上限 {MaxStates}");
                    to = dfa.StateCount;
                    ids[key] = to;
                    dfa.AddState(next);
                    queue.Enqueue(next);
                }
                dfa._trans[from].Add((lo, hi, to));
            }
        }
        return dfa;
    }

    private void AddState(HashSet<Nfa.State> set)
    {
        int accept = -1;
        foreach (var s in set)
            if (s.AcceptRule >= 0 && (accept < 0 || s.AcceptRule < accept))
                accept = s.AcceptRule;
        _accept.Add(accept);
        _trans.Add([]);
    }

    private static string Key(HashSet<Nfa.State> set) =>
        string.Join(',', set.Select(s => s.GetHashCode()).OrderBy(x => x));

    private static HashSet<Nfa.State> EpsilonClosure(IEnumerable<Nfa.State> seeds)
    {
        var result = new HashSet<Nfa.State>();
        var stack = new Stack<Nfa.State>(seeds);
        while (stack.Count > 0)
        {
            var s = stack.Pop();
            if (!result.Add(s)) continue;
            foreach (var e in s.Epsilon)
                stack.Push(e);
        }
        return result;
    }

    // 将集合内所有字符边按覆盖区间切分为不相交段，每段合并目标集合。
    private static List<(char Lo, char Hi, List<Nfa.State> Targets)> CollectMoves(HashSet<Nfa.State> set)
    {
        var bounds = new SortedSet<int>();
        var edges = new List<(int Lo, int Hi, Nfa.State To)>();
        foreach (var s in set)
            foreach (var (lo, hi, to) in s.Moves)
            {
                edges.Add((lo, hi, to));
                bounds.Add(lo);
                bounds.Add(hi + 1);
            }
        var result = new List<(char, char, List<Nfa.State>)>();
        var points = bounds.ToArray();
        for (int i = 0; i + 1 < points.Length; i++)
        {
            int lo = points[i], hi = points[i + 1] - 1;
            var targets = new List<Nfa.State>();
            foreach (var (elo, ehi, to) in edges)
                if (elo <= lo && ehi >= hi)
                    targets.Add(to);
            if (targets.Count > 0)
                result.Add(((char)lo, (char)hi, targets));
        }
        return result;
    }

    /// <summary>从 state 出发消费 c，返回下一状态，死路返回 -1。</summary>
    public int Step(int state, char c)
    {
        foreach (var (lo, hi, to) in _trans[state])
            if (c >= lo && c <= hi)
                return to;
        return -1;
    }

    public int Accept(int state) => _accept[state];
}
