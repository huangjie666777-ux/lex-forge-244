using LexForge244;

int passed = 0;

void Check(bool cond, string name)
{
    if (!cond) throw new Exception($"FAIL: {name}");
    passed++;
    Console.WriteLine($"ok - {name}");
}

void Throws(Action a, string name, string? expect = null)
{
    try { a(); }
    catch (LexException e)
    {
        if (expect != null && !e.Message.Contains(expect))
            throw new Exception($"FAIL: {name} (消息不含 '{expect}': {e.Message})");
        passed++;
        Console.WriteLine($"ok - {name}");
        return;
    }
    throw new Exception($"FAIL: {name} (未抛出 LexException)");
}

// 1. 关键字与标识符重叠: 同长取先声明者，更长取最长匹配
var lexer = Lexer.Compile(new[]
{
    new LexerRule("IF", "if"),
    new LexerRule("ID", "[a-z][a-z0-9]*"),
    new LexerRule("NUM", "[0-9]+"),
    new LexerRule("WS", "[ \t\n]+", Skip: true),
    new LexerRule("ASSIGN", "="),
    new LexerRule("EQ", "=="),
});
var toks = lexer.Tokenize("if iffy x = 42\ny == x");
Check(toks.Count == 8, "词法单元数量");
Check(toks[0] is { Name: "IF", Text: "if", Offset: 0, Length: 2, Line: 1, Column: 1 }, "关键字优先于标识符");
Check(toks[1] is { Name: "ID", Text: "iffy" }, "最长匹配覆盖关键字前缀");
Check(toks[3] is { Name: "ASSIGN", Text: "=" }, "单等号");
Check(toks[4] is { Name: "NUM", Text: "42" }, "数字");
Check(toks[5] is { Name: "ID", Text: "y", Line: 2, Column: 1 }, "跳过空白仍推进行列");
Check(toks[6] is { Name: "EQ", Text: "==" }, "最长匹配双等号");
Check(toks[7] is { Name: "ID", Text: "x", Line: 2, Column: 6 }, "行列随换行更新");

// 2. 可重复扫描，不保留状态，不受规则列表后续修改影响
var mutable = new List<LexerRule> { new("A", "a+") };
var lex2 = Lexer.Compile(mutable);
mutable.Add(new LexerRule("B", "b+"));
Check(lex2.Tokenize("aa").Count == 1 && lex2.Tokenize("aa").Count == 1, "重复扫描结果一致");
Throws(() => lex2.Tokenize("b"), "编译后修改规则列表不生效");

// 3. 死路回退: 余下字符重新参与下一单元
var lex3 = Lexer.Compile(new[]
{
    new LexerRule("AB", "ab"),
    new LexerRule("A", "a"),
    new LexerRule("B", "b"),
    new LexerRule("C", "c"),
});
var t3 = lex3.Tokenize("abc");
Check(t3.Count == 2 && t3[0].Text == "ab" && t3[1].Text == "c", "正常分段");
var lex3b = Lexer.Compile(new[]
{
    new LexerRule("ABC", "abc"),
    new LexerRule("AB", "ab"),
    new LexerRule("D", "d"),
});
var t3b = lex3b.Tokenize("abd");
Check(t3b.Count == 2 && t3b[0].Name == "AB" && t3b[1].Name == "D", "死路退回最后接受位置");

// 4. 空文本
Check(lexer.Tokenize("").Count == 0, "空文本返回空结果");

// 5. 错误输入: 首个未识别位置，停止不跳字
try
{
    lexer.Tokenize("x ? y");
    throw new Exception("FAIL: 应报错");
}
catch (LexException e)
{
    Check(e.TextOffset == 2 && e.Line == 1 && e.Column == 3, "未识别位置准确");
}
try
{
    lexer.Tokenize("x\né");
    throw new Exception("FAIL: 应报非 ASCII");
}
catch (LexException e)
{
    Check(e.TextOffset == 2 && e.Line == 2 && e.Column == 1 && e.Message.Contains("U+00E9"), "非 ASCII 位置准确");
}

// 6. 非法语法与规则校验
Throws(() => Lexer.Compile(new[] { new LexerRule("R", "a.c") }), "拒绝通配点", "通配点");
Throws(() => Lexer.Compile(new[] { new LexerRule("R", "[^a]") }), "拒绝否定类", "否定");
Throws(() => Lexer.Compile(new[] { new LexerRule("R", "^a") }), "拒绝锚点", "锚点");
Throws(() => Lexer.Compile(new[] { new LexerRule("R", "a{2}") }), "拒绝计数重复", "计数重复");
Throws(() => Lexer.Compile(new[] { new LexerRule("R", "(a)\\1") }), "拒绝反向引用", "反向引用");
Throws(() => Lexer.Compile(new[] { new LexerRule("R", "a*") }), "拒绝可空规则", "空串");
Throws(() => Lexer.Compile(new[] { new LexerRule("R", "a?b?") }), "拒绝可空规则2", "空串");
Throws(() => Lexer.Compile(new[] { new LexerRule("R", "[z-a]") }), "拒绝降序范围", "升序");
Throws(() => Lexer.Compile(new[] { new LexerRule("R", "(a") }), "缺少右括号", "右括号");
Throws(() => Lexer.Compile(new[] { new LexerRule("R", "a|") }), "空分支", "空分支");
Throws(() => Lexer.Compile(new[] { new LexerRule("R", "é") }), "模式非 ASCII", "ASCII");
Throws(() => Lexer.Compile(new[] { new LexerRule("R", "a"), new LexerRule("R", "b") }), "重名", "重复");
Throws(() => Lexer.Compile(new[] { new LexerRule("", "a") }), "空名", "不能为空");
Throws(() => Lexer.Compile(Array.Empty<LexerRule>()), "空规则集", "至少需要一条");

// 错误信息含规则名与偏移
try
{
    Lexer.Compile(new[] { new LexerRule("MyRule", "ab.c") });
    throw new Exception("FAIL");
}
catch (LexException e)
{
    Check(e.RuleName == "MyRule" && e.PatternOffset == 2, "错误携带规则名与偏移");
}

// 7. 转义与分组、选择、重复优先级
var lex7 = Lexer.Compile(new[]
{
    new LexerRule("S", "(ab|cd)+e?"),
    new LexerRule("NL", "\\n"),
    new LexerRule("TAB", "\\t"),
    new LexerRule("STAR", "\\*"),
});
var t7 = lex7.Tokenize("abcdabe\n\t*");
Check(t7[0] is { Name: "S", Text: "abcdabe" }, "分组选择重复组合");
Check(t7[1].Name == "NL" && t7[2].Name == "TAB" && t7[3].Name == "STAR", "转义字符");

// 8. 同长规则顺序优先
var lex8 = Lexer.Compile(new[]
{
    new LexerRule("FIRST", "[a-c]+"),
    new LexerRule("SECOND", "[a-c]+"),
});
Check(lex8.Tokenize("abc")[0].Name == "FIRST", "同长取先声明规则");

Console.WriteLine($"\n全部 {passed} 项断言通过");
