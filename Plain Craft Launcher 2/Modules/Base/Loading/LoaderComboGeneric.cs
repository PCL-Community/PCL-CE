using Microsoft.VisualBasic.CompilerServices;

namespace PCL;

public static partial class Loading
{
    /// <summary>
    ///     支持多个加载器连续运作的复合加载器（泛型版本）。
    /// </summary>
    public class LoaderCombo<InputType>(
        string name,
        IEnumerable<LoaderBase> loaders,
        InputType? typedInput = default) : LoaderCombo(name, loaders)
    {
        public InputType? typedInput = typedInput;

        public override void Start(object? input = null, bool isForceRestart = false)
        {
            typedInput = Conversions.ToGenericParameter<InputType>(input);
            base.Start(typedInput, isForceRestart);
        }
    }
}
