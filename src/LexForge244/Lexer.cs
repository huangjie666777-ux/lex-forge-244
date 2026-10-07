namespace LexForge244;

/// <summary>
/// 编译后的词法器。线程安全、无状态，可反复扫描不同完整文本。
/// 编译期完成模式解析、Thompson NFA 构造与共享 DFA 子集构造，之后与规则列表无关。
/// </summary>
public sealed class Lexer
{
    private readonly Dfa _dfa;
    private readonly string[] _names;
    private readonly bool[] _skips;

    private Lexer(Dfa dfa, string[] names, bool[] skips)
    {
        _dfa = dfa;
        _names = names;
        _skips = skips;
    }

    /// <summary>按优先级顺序编译规则。任何规则非法都会抛出 <see cref="LexException"/>，不交付半成品。</summary>
    public static Lexer Compile(IEnumerable<LexerRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var list = rules.ToList();
        if (list.Count == 0)
            throw new LexException("至少需要一条规则");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var parsed = new List<(Node Ast, int Priority)>();
        var names = new string[list.Count];
        var skips = new bool[list.Count];

        for (int i = 0; i < list.Count; i++)
        {
            var rule = list[i] ?? throw new LexException($"规则 #{i} 为 null");
            if (string.IsNullOrEmpty(rule.Name))
                throw new LexException($"规则 #{i} 的名称不能为空");
            if (!seen.Add(rule.Name))
                throw new LexException($"规则名 '{rule.Name}' 重复");
            var ast = RegexParser.Parse(rule.Pattern, rule.Name);
            if (Node.Nullable(ast))
                throw new LexException("该规则可匹配空串，不允许", rule.Name, 0);
            parsed.Add((ast, i));
            names[i] = rule.Name;
            skips[i] = rule.Skip;
        }

        var nfa = Nfa.Build(parsed);
        var dfa = Dfa.Build(nfa);
        return new Lexer(dfa, names, skips);
    }

    /// <summary>
    /// 扫描完整文本，返回全部词法单元(跳过规则不产生单元但仍推进位置)。
    /// 每次调用独立，不保留上次状态。空文本返回空结果。
    /// 无法匹配或遇非 ASCII 字符时抛出携带首个未识别位置的 <see cref="LexException"/>。
    /// </summary>
    public IReadOnlyList<Token> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = new List<Token>();
        int pos = 0, line = 1, col = 1;

        while (pos < text.Length)
        {
            int state = 0;
            int lastAcceptPos = -1, lastAcceptRule = -1;
            int cur = pos;

            while (cur < text.Length)
            {
                char c = text[cur];
                if (c > 0x7F)
                    break; // 非 ASCII: 先结算已匹配部分，再报错
                int next = _dfa.Step(state, c);
                if (next < 0)
                    break;
                state = next;
                cur++;
                int accept = _dfa.Accept(state);
                if (accept >= 0)
                {
                    lastAcceptPos = cur;
                    lastAcceptRule = accept;
                }
            }

            if (lastAcceptPos < 0)
            {
                char bad = text[pos];
                string why = bad > 0x7F ? $"非 ASCII 字符 U+{(int)bad:X4}" : $"无法识别字符 '{bad}'";
                throw new LexException(why, pos, line, col);
            }

            int length = lastAcceptPos - pos;
            if (!_skips[lastAcceptRule])
            {
                tokens.Add(new Token(
                    _names[lastAcceptRule],
                    text.Substring(pos, length),
                    pos, length, line, col));
            }
            for (int i = pos; i < lastAcceptPos; i++)
            {
                if (text[i] == '\n') { line++; col = 1; }
                else col++;
            }
            pos = lastAcceptPos;
        }
        return tokens;
    }
}
