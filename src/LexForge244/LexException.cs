namespace LexForge244;

/// <summary>词法编译或扫描失败时抛出，携带规则名与位置信息。</summary>
public sealed class LexException : Exception
{
    public LexException(string message) : base(message) { }

    public LexException(string message, string ruleName, int patternOffset)
        : base($"规则 '{ruleName}' 的模式在偏移 {patternOffset} 处无效: {message}")
    {
        RuleName = ruleName;
        PatternOffset = patternOffset;
    }

    public LexException(string message, int textOffset, int line, int column)
        : base($"扫描失败于偏移 {textOffset} (行 {line}, 列 {column}): {message}")
    {
        TextOffset = textOffset;
        Line = line;
        Column = column;
    }

    public string? RuleName { get; }
    public int? PatternOffset { get; }
    public int? TextOffset { get; }
    public int? Line { get; }
    public int? Column { get; }
}
