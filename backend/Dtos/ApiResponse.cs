using System.Text.Json.Serialization;

namespace backend.Dtos;

public class ApiResponse<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public T? Data { get; set; }
    public static ApiResponse<T> Success(T data, int code = 0, string message = "Ok")
    {
        return new ApiResponse<T> { Code = code, Message = message, Data = data };
    }
    public static ApiResponse<T> Fail(int code, string message)
    {
        return new ApiResponse<T> { Code = code, Message = message, Data = default };
    }
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Success(int code = 0, string message = "Ok")
    {
        return new ApiResponse { Code = code, Message = message, Data = null };
    }
    public new static ApiResponse Fail(int code, string message)
    {
        return new ApiResponse { Code = code, Message = message, Data = null };
    }
}
