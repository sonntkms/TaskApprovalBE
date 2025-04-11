namespace TaskApprovalBE.Models;

public class ResponseMessage(string msg)
{
    public string Message { get; private set; } = msg;
}