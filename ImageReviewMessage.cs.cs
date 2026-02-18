using System;

namespace GardenImageReviewFunction;

public class ImageReviewMessage
{
    /// <summary>
    /// The ID of the plant associated with the image to review. Must be a positive integer.
    /// </summary>
    public int PlantId { get; set; }

    /// <summary>
    /// A SAS URL pointing to the image that needs to be reviewed. Must be a non-empty string.
    /// </summary>
    public string? ReviewImageSasUrl { get; set; }
}
