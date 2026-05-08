namespace Maintenance.Core.Models
{
    public class MailModel
        {
            public string to { get; set; }
            public string from { get; set; }
            public string subject { get; set; }
            public string content { get; set; }
            public int isHtml { get; set; }
            public bool now { get; set; }
            public string fromName { get; set; }
            public string attachment { get; set; }
            public bool withReintento { get; set; }
        }
}
