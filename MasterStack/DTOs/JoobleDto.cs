public class JoobleRequestDto
{
    public string keywords { get; set; } = string.Empty;
    public string location { get; set; } = string.Empty;
    public int radius { get; set; } = 25;
}