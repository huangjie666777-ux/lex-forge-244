# LexForge244

面向脚本宿主的词法规则编译库：一次编译规则集，反复扫描不同完整文本。
C# 12 / .NET 8，零依赖，不调用 `Regex` 或任何现成生成器。

## 功能

- 规则按优先级排列，每条含唯一非空名称、正则模式、是否跳过(`Skip`)。
- 模式语法：ASCII 字面量、反斜杠转义(`\n` `\r` `\t` 及元字符)、
  字符类及升序范围(`[a-z0-9_]`)、圆括号分组、连接、`|`、`*`、`+`、`?`。
  优先级：重复 > 连接 > 选择。
- 不支持：通配点、否定类、锚点、计数重复、反向引用；非法语法抛出携带
  规则名与模式偏移的 `LexException`。
- 自行构建 Thompson NFA，再经含 epsilon 闭包的子集构造生成共享 DFA。
- 可匹配空串的规则在编译期拒绝；NFA 与 DFA 各限 4096 状态，超限明确失败。
- 扫描：从当前位置取最长匹配，同长按规则顺序；死路或文本结束时退回最后
  接受位置，余下字符重新参与下一词法单元。
- 词法单元含名称、原词文、从 0 起的偏移与长度、从 1 起的行列(仅 LF 换行)；
  跳过规则仍推进位置。无法匹配或遇非 ASCII 字符时报首个未识别位置并停止；
  空文本成功返回空结果。

## 公共接口

```csharp
public sealed record LexerRule(string Name, string Pattern, bool Skip = false);
public sealed record Token(string Name, string Text, int Offset, int Length, int Line, int Column);
public sealed class Lexer
{
    public static Lexer Compile(IEnumerable<LexerRule> rules); // 编译期校验, 失败抛 LexException
    public IReadOnlyList<Token> Tokenize(string text);          // 无状态, 可反复调用
}
public sealed class LexException : Exception { /* RuleName/PatternOffset 或 TextOffset/Line/Column */ }
```

`Lexer` 实例不可变、线程安全；编译后与调用方的规则列表无关。

## 构建与运行

```bash
dotnet build                                   # 构建全部项目
dotnet run --project src/LexForge244.Tests     # 运行自测(33 项断言)
dotnet run --project src/LexForge244.Demo      # 运行调用示例(关键字/标识符重叠、跳过空白、错误输入)
```

## 结构

- `src/LexForge244/RegexParser.cs` — 受限正则解析为 AST，报告规则名与偏移
- `src/LexForge244/Nfa.cs` — Thompson 构造，4096 状态上限
- `src/LexForge244/Dfa.cs` — epsilon 闭包子集构造，接受态取最小规则优先级
- `src/LexForge244/Lexer.cs` — 公共入口与最长匹配扫描器
- `src/LexForge244.Tests` — 自测；`src/LexForge244.Demo` — 调用示例
