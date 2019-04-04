using System;
using System.Collections.Generic;
using System.Text;

namespace MLAPI.CertificateGeneratorCommon
{
    public class GenerationLog
    {
        public List<LogEntry> Entries = new List<LogEntry>();
        public DateTime StartTime = DateTime.UtcNow;
        public DateTime EndTime;

        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("## Log");
            
            sb.AppendLine("```");
            for (int i = 0; i < Entries.Count; i++)
            {
                sb.AppendLine("[" + Entries[i].Time + " (UTC)]: " + Entries[i].Text + " +" + (Entries[i].Time - StartTime).TotalMilliseconds + "ms");
            }
            sb.AppendLine("[" + EndTime + " (UTC)]: Completed in total of " + (EndTime - StartTime).TotalMilliseconds + " ms");

            sb.AppendLine("```");

            return sb.ToString();
        }
    }

    public class LogEntry
    {
        public LogEntry(string text)
        {
            Text = text;
            Console.WriteLine(text);
        }
        
        public string Text;
        public DateTime Time = DateTime.UtcNow;
    }
}