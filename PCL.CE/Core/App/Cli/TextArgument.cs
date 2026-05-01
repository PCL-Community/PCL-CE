namespace PCL.CE.Core.App.Cli;

public class TextArgument : CommandArgument<string>
{
    public override ArgumentValueKind ValueKind => ArgumentValueKind.Text;

    protected override string ParseValueText() => ValueText;
}
