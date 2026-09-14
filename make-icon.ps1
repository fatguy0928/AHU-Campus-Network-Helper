param(
    [string]$Source = (Join-Path $PSScriptRoot 'assets\ahu-official.jpg'),
    [string]$Destination = (Join-Path $PSScriptRoot 'assets\ahu.ico')
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing

$sourceBitmap=New-Object System.Drawing.Bitmap((Resolve-Path -LiteralPath $Source).Path)
try
{
    if($sourceBitmap.Width -lt 202 -or $sourceBitmap.Height -lt 203) {throw 'Official logo source is smaller than expected.'}
    $master=New-Object System.Drawing.Bitmap(1024,1024,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $square=New-Object System.Drawing.Bitmap(1024,1024,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        $graphics=[System.Drawing.Graphics]::FromImage($square)
        try
        {
            $graphics.Clear([System.Drawing.Color]::White)
            $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($sourceBitmap,(New-Object System.Drawing.Rectangle(0,0,1024,1024)),8,9,194,194,[System.Drawing.GraphicsUnit]::Pixel)
        }
        finally {$graphics.Dispose()}

        $graphics=[System.Drawing.Graphics]::FromImage($master)
        try
        {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $brush=New-Object System.Drawing.TextureBrush($square,[System.Drawing.Drawing2D.WrapMode]::Clamp)
            try {$graphics.FillEllipse($brush,1,1,1022,1022)} finally {$brush.Dispose()}
        }
        finally {$graphics.Dispose()}

        $sizes=@(16,24,32,48,64,128,256)
        $images=New-Object 'System.Collections.Generic.List[byte[]]'
        foreach($size in $sizes)
        {
            $bitmap=New-Object System.Drawing.Bitmap($size,$size,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics=[System.Drawing.Graphics]::FromImage($bitmap)
            try
            {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.DrawImage($master,0,0,$size,$size)
            }
            finally {$graphics.Dispose()}
            try
            {
                $memory=New-Object System.IO.MemoryStream
                try {$bitmap.Save($memory,[System.Drawing.Imaging.ImageFormat]::Png);$images.Add($memory.ToArray())}
                finally {$memory.Dispose()}
                if($size -eq 256) {$bitmap.Save((Join-Path (Split-Path -Parent $Destination) 'ahu-icon-preview.png'),[System.Drawing.Imaging.ImageFormat]::Png)}
            }
            finally {$bitmap.Dispose()}
        }

        $directory=Split-Path -Parent $Destination
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        $stream=[System.IO.File]::Create($Destination)
        $writer=New-Object System.IO.BinaryWriter($stream)
        try
        {
            $writer.Write([UInt16]0);$writer.Write([UInt16]1);$writer.Write([UInt16]$sizes.Count)
            $offset=6+16*$sizes.Count
            for($i=0;$i -lt $sizes.Count;$i++)
            {
                $dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
                $writer.Write([Byte]$dimension);$writer.Write([Byte]$dimension);$writer.Write([Byte]0);$writer.Write([Byte]0)
                $writer.Write([UInt16]1);$writer.Write([UInt16]32)
                $writer.Write([UInt32]$images[$i].Length);$writer.Write([UInt32]$offset)
                $offset+=$images[$i].Length
            }
            foreach($bytes in $images) {$writer.Write($bytes)}
        }
        finally {$writer.Dispose()}
    }
    finally {$square.Dispose();$master.Dispose()}
}
finally {$sourceBitmap.Dispose()}
