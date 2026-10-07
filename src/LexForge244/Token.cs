namespace LexForge244;

/// <summary>一个词法单元：规则名、原词文、从 0 起的偏移与长度、从 1 起的行列。</summary>
public sealed record Token(string Name, string Text, int Offset, int Length, int Line, int Column);
