using System.Runtime.CompilerServices;
namespace Rustic.Memory.Data.Linq;

public ref struct InlineStack<T>
{
    private readonly Span<T> _stack;
    private int _stackIndex;

    public OperatorTreeEvaluationFinateStateMaschine(Span<T> stack)
    {
        _stack = stack;
        _stackIndex = 0;
    }

    public ReadOnlySpan<T> Stack => _stack[.._stackIndex];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(T value)
    {
        _stack[_stackIndex++] = value;
    }

    public T? TryPop()
    {
        return _stackIndex > 0 ? _stack[--_stackIndex] : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Pop()
    {
        return _stack[--_stackIndex];
    }

    public T? TryPeek()
    {
        return _stackIndex > 0 ? _stack[_stackIndex - 1] : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEmpty => _stackIndex == 0;

}

