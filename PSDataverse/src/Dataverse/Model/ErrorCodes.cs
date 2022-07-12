namespace DataverseModule.Dataverse.Model;

public class ErrorCode
{
    public int Code { get; set; }
    public string Message { get; set; }

    public static Dictionary<int, string> Samples =
    {
        [0x80060891] = "A record with the specified key values does not exist in email entity",
        [0x80044339] = "Cannot obtain lock on resource:'Email_77f15f8b-5cf8-ec11-bb3d-000d3ade7928', mode:Update - stored procedure sp_getapplock returned error code -3. The lock request was chosen as a deadlock victim. This can occur when there are multiple update requests on the same record. Please retry the request.",
        [0x80040278] = "Invalid character in field 'description': '\b', hexadecimal value 0x08, is an invalid character."
        [0x80040216] = "Create failed for the attachment"
        [0x80043e09] = "The attachment is either not a valid type or is too large. It cannot be uploaded or downloaded."
        [0x80048105] = "More than one concurrent GrantInheritedAccess requests detected for an Entity adf2ddb2-c97e-4cdc-986e-051d6e26f823 and ObjectTypeCode 380."
    };
}