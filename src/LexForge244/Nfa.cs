namespace LexForge244;

/// <summary>Thompson 构造的 NFA。状态带 epsilon 边与字符区间边，终态记录规则优先级。</summary>
internal sealed class Nfa
{
    public const int MaxStates = 4096;

    internal sealed class State
    {
        public List<State> Epsilon = [];
        public List<(char Lo, char Hi, State To)> Moves = [];
        public int AcceptRule = -1; // >=0 表示接受态，值为规则优先级(越小越优先)
    }

    public List<State> States { get; } = [];
    public State Start { get; }

    private Nfa(State start) => Start = start;

    public static Nfa Build(IReadOnlyList<(Node Ast, int Priority)> rules)
    {
        var nfa = new Nfa(new State());
        nfa.States.Add(nfa.Start);
        foreach (var (ast, priority) in rules)
        {
            var frag = nfa.BuildFrag(ast);
            frag.Accept.AcceptRule = priority;
            nfa.Start.Epsilon.Add(frag.Start);
        }
        return nfa;
    }

    private State NewState()
    {
        if (States.Count >= MaxStates)
            throw new LexException($"NFA 状态数超过上限 {MaxStates}");
        var s = new State();
        States.Add(s);
        return s;
    }

    private (State Start, State Accept) BuildFrag(Node node)
    {
        switch (node)
        {
            case Node.Chars ch:
            {
                var s = NewState();
                var a = NewState();
                foreach (var (lo, hi) in ch.Ranges)
                    s.Moves.Add((lo, hi, a));
                return (s, a);
            }
            case Node.Cat cat:
            {
                var first = BuildFrag(cat.Items[0]);
                var cur = first;
                for (int i = 1; i < cat.Items.Count; i++)
                {
                    var next = BuildFrag(cat.Items[i]);
                    cur.Accept.Epsilon.Add(next.Start);
                    cur = (cur.Start, next.Accept);
                }
                return cur;
            }
            case Node.Alt alt:
            {
                var s = NewState();
                var a = NewState();
                foreach (var branch in alt.Branches)
                {
                    var f = BuildFrag(branch);
                    s.Epsilon.Add(f.Start);
                    f.Accept.Epsilon.Add(a);
                }
                return (s, a);
            }
            case Node.Star star:
            {
                var s = NewState();
                var a = NewState();
                var inner = BuildFrag(star.Inner);
                s.Epsilon.Add(inner.Start);
                s.Epsilon.Add(a);
                inner.Accept.Epsilon.Add(inner.Start);
                inner.Accept.Epsilon.Add(a);
                return (s, a);
            }
            case Node.Plus plus:
            {
                var inner = BuildFrag(plus.Inner);
                var a = NewState();
                inner.Accept.Epsilon.Add(inner.Start);
                inner.Accept.Epsilon.Add(a);
                return (inner.Start, a);
            }
            case Node.Opt opt:
            {
                var s = NewState();
                var a = NewState();
                var inner = BuildFrag(opt.Inner);
                s.Epsilon.Add(inner.Start);
                s.Epsilon.Add(a);
                inner.Accept.Epsilon.Add(a);
                return (s, a);
            }
            default:
                throw new InvalidOperationException();
        }
    }
}
