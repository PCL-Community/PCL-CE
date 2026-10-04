using System;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Models.Enums;

[Serializable]
public enum CompProjectStatus
{
    Approved,
    Draft,
    Rejected,
    Archived,
    Unlisted,
    Processing,
    Withheld,
    Scheduled,
    Unknown
}
