using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.Common.Automation;

public class UpdateChannelRequest
{
    public string Channel { get; set; } = string.Empty; // "whatsapp" | "email"
    public bool Enabled { get; set; }
}


public class UpdateSkipDaysRequest
{
    public int SkipDays { get; set; }
}