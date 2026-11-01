using System.Text;

namespace CopperCalzone.Parsing;

public static class SExpressionParser
{
    public static SExpNode Parse(ReadOnlySpan<char> text)
    {
        var stack = new Stack<List<SExpNode>>();
        var root = new List<SExpNode>();
        stack.Push(root);

        int i = 0;
        int len = text.Length;
        var sb = new StringBuilder();

        while (i < len)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '(')
            {
                var newList = new List<SExpNode>();
                stack.Peek().Add(new SExpNode(newList));
                stack.Push(newList);
                i++;
            }
            else if (c == ')')
            {
                if (stack.Count > 1)
                {
                    stack.Pop();
                }
                i++;
            }
            else if (c == '"')
            {
                sb.Clear();
                i++; // skip opening quote
                while (i < len)
                {
                    char ch = text[i];
                    if (ch == '\\' && i + 1 < len)
                    {
                        char next = text[i + 1];
                        sb.Append(next);
                        i += 2;
                    }
                    else if (ch == '"')
                    {
                        i++; // skip closing quote
                        break;
                    }
                    else
                    {
                        sb.Append(ch);
                        i++;
                    }
                }
                stack.Peek().Add(new SExpNode(sb.ToString()));
            }
            else
            {
                int start = i;
                while (i < len && !char.IsWhiteSpace(text[i]) && text[i] != '(' && text[i] != ')')
                {
                    i++;
                }
                string token = text.Slice(start, i - start).ToString();
                stack.Peek().Add(new SExpNode(token));
            }
        }

        return root.Count > 0 ? root[0] : new SExpNode(new List<SExpNode>());
    }
}