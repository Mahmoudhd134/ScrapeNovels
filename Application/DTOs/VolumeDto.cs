﻿namespace Application.DTOs;

public class VolumeDto
{
    public string Title { get; set; }
    public IList<ChapterDto> Chapters { get; set; }
}