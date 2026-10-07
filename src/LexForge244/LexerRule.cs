namespace LexForge244;

/// <summary>一条词法规则。名称唯一非空，模式为受限正则，Skip 表示匹配后丢弃。</summary>
public sealed record LexerRule(string Name, string Pattern, bool Skip = false);
