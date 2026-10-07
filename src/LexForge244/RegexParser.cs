namespace LexForge244;

/// <summary>
/// 受限正则解析器。支持 ASCII 字面量、反斜杠转义(n/r/t/元字符)、字符类及升序范围、
/// 分组、连接、|、*、+、?。不支持通配点、否定类、锚点、计数重复、反向引用。
/// 语法错误抛出携带规则名与模式偏移的 <see cref="LexException"/>。
/// </summary>
internal sealed class RegexParser
{
    private const string MetaChars = "\\[]()|*+?-";

    private readonly string _pattern;
    private readonly string _ruleName;
    private int _pos;

    private RegexParser(string pattern, string ruleName)
    {
        _pattern = pattern;
        _ruleName = ruleName;
    }

    public static Node Parse(string pattern, string ruleName)
    {
        if (string.IsNullOrEmpty(pattern))
            throw new LexException("模式不能为空", ruleName, 0);
        var parser = new RegexParser(pattern, ruleName);
        var node = parser.ParseAlt();
        if (parser._pos != parser._pattern.Length)
            throw parser.Error($"意外的字符 '{parser._pattern[parser._pos]}'");
        return node;
    }

    private LexException Error(string message) => new(message, _ruleName, _pos);

    private bool AtEnd => _pos >= _pattern.Length;
    private char Peek => _pattern[_pos];

    // alt := concat ('|' concat)*
    private Node ParseAlt()
    {
        var branches = new List<Node> { ParseCat() };
        while (!AtEnd && Peek == '|')
        {
            _pos++;
            branches.Add(ParseCat());
        }
        return branches.Count == 1 ? branches[0] : new Node.Alt(branches);
    }

    // concat := repeat*
    private Node ParseCat()
    {
        var items = new List<Node>();
        while (!AtEnd && Peek != '|' && Peek != ')')
            items.Add(ParseRepeat());
        if (items.Count == 0)
            throw Error("此处不允许空分支或空分组");
        return items.Count == 1 ? items[0] : new Node.Cat(items);
    }

    // repeat := atom ('*'|'+'|'?')*
    private Node ParseRepeat()
    {
        var atom = ParseAtom();
        while (!AtEnd && Peek is '*' or '+' or '?')
        {
            var op = _pattern[_pos++];
            atom = op switch
            {
                '*' => new Node.Star(atom),
                '+' => new Node.Plus(atom),
                _ => new Node.Opt(atom),
            };
        }
        return atom;
    }

    private Node ParseAtom()
    {
        if (AtEnd)
            throw Error("模式意外结束");
        char c = Peek;
        switch (c)
        {
            case '(':
            {
                _pos++;
                var inner = ParseAlt();
                if (AtEnd || Peek != ')')
                    throw Error("缺少右括号 ')'");
                _pos++;
                return inner;
            }
            case ')':
                throw Error("多余的右括号 ')'");
            case '[':
                return ParseClass();
            case '\\':
                return ParseEscape();
            case '.' or '^' or '$':
                throw Error($"不支持 '{c}'(不支持通配点与锚点)");
            case '{' or '}':
                throw Error("不支持计数重复 '{...}'");
            case '*' or '+' or '?':
                throw Error($"重复符 '{c}' 前没有可重复的单元");
            default:
                _pos++;
                CheckAscii(c);
                return new Node.Chars([(c, c)]);
        }
    }

    private Node ParseEscape()
    {
        _pos++; // 跳过反斜杠
        if (AtEnd)
            throw Error("反斜杠后缺少转义字符");
        char c = _pattern[_pos++];
        char literal = c switch
        {
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            '0' => throw new LexException("不支持空字符转义", _ruleName, _pos - 2),
            _ when c is >= '0' and <= '9' =>
                throw new LexException("不支持反向引用", _ruleName, _pos - 2),
            _ when MetaChars.Contains(c) => c,
            _ => throw new LexException($"不支持的转义 '\\{c}'", _ruleName, _pos - 2),
        };
        return new Node.Chars([(literal, literal)]);
    }

    private Node ParseClass()
    {
        _pos++; // 跳过 '['
        if (!AtEnd && Peek == '^')
            throw Error("不支持否定字符类");
        var ranges = new List<(char, char)>();
        bool first = true;
        while (true)
        {
            if (AtEnd)
                throw Error("字符类缺少 ']'");
            if (Peek == ']' && !first)
            {
                _pos++;
                break;
            }
            first = false;
            char lo = ReadClassChar();
            if (!AtEnd && Peek == '-' && _pos + 1 < _pattern.Length && _pattern[_pos + 1] != ']')
            {
                _pos++;
                char hi = ReadClassChar();
                if (hi < lo)
                    throw new LexException(
                        $"范围 '{lo}-{hi}' 必须升序", _ruleName, _pos - 3);
                ranges.Add((lo, hi));
            }
            else
            {
                ranges.Add((lo, lo));
            }
        }
        if (ranges.Count == 0)
            throw Error("字符类不能为空");
        return new Node.Chars(ranges);
    }

    private char ReadClassChar()
    {
        char c = _pattern[_pos++];
        if (c == '\\')
        {
            if (AtEnd)
                throw Error("反斜杠后缺少转义字符");
            char e = _pattern[_pos++];
            return e switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ when MetaChars.Contains(e) => e,
                _ => throw new LexException($"不支持的转义 '\\{e}'", _ruleName, _pos - 2),
            };
        }
        CheckAscii(c, _pos - 1);
        return c;
    }

    private void CheckAscii(char c, int? offset = null)
    {
        if (c > 0x7F)
            throw new LexException("仅支持 ASCII 字符", _ruleName, offset ?? _pos - 1);
    }
}
