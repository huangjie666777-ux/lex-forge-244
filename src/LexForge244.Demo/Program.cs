using LexForge244;

// 规则按优先级排列: 关键字先于标识符, 跳过规则置 Skip
var lexer = Lexer.Compile(new[]
{
    new LexerRule("IF", "if"),
    new LexerRule("ELSE", "else"),
    new LexerRule("WHILE", "while"),
    new LexerRule("ID", "[a-zA-Z_][a-zA-Z0-9_]*"),
    new LexerRule("NUM", "[0-9]+"),
    new LexerRule("OP", """\+|\-|\*|/|==|="""),
    new LexerRule("LPAREN", """\("""),
    new LexerRule("RPAREN", """\)"""),
    new LexerRule("SEMI", ";"),
    new LexerRule("WS", "[ \t\r\n]+", Skip: true),
});

const string source = """
if (x == 1)
    y = y + 20;
else
    y = 0;
""";

Console.WriteLine("== 正常扫描 ==");
foreach (var t in lexer.Tokenize(source))
    Console.WriteLine($"{t.Name,-6} '{t.Text}' 偏移={t.Offset} 长度={t.Length} 行={t.Line} 列={t.Column}");

Console.WriteLine("\n== 错误输入 ==");
try
{
    lexer.Tokenize("x = 1; @");
}
catch (LexException e)
{
    Console.WriteLine(e.Message);
}
