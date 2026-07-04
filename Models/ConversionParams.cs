namespace RecurOpus.Models;

public class ConversionParams
{
    public required string FfmpegPath { get; set; }
    
    public required string OpusencPath { get; set; }
    
    public required string InputPath { get; set; }
    
    public required string OutputPath { get; set; }
    
    public required int Bitrate { get; set; }
    
    public required int AlbumArtSize { get; set; }
}
