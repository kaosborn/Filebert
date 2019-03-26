using System.IO;
using KaosIssue;

namespace KaosFormat
{
    public class UnknownFormat : FormatBase
    {
        public static string[] SNames => new string[] { "*unknown*" };
        public override string[] Names => SNames;

        public new class Model : FormatBase.Model
        {
            public new readonly UnknownFormat Data;

            public Model (Stream stream, string path)
            {
                base._data = Data = new UnknownFormat (this, stream, path);
                IssueModel.Add ("Unknown extension.", Severity.Trivia);
            }
        }


        public UnknownFormat (Model model, Stream stream, string path) : base (model, stream, path)
        { }
    }
}
