namespace SstvVPrinter.Imaging.Composition;

public sealed class RadiofaxPageShapeException : Exception
{
    public RadiofaxPageShapeException()
        : base("Radiofaxで送信できる画像の縦横比は1:2から2:1までです。回転するか画像を破棄してください。")
    {
    }
}
