float2 mousePosition : register(C1);
float radius : register(C2);
float squareSize : register(c3);
float textureSize : register(c4);

sampler2D inputSampler : register(S0);

float4 main(float2 uv
            : TEXCOORD) :
    COLOR
{
    // do not draw grid where the mouse is
    if (uv.x == mousePosition.y && uv.y == mousePosition.y)
    {
        return tex2D(inputSampler, uv);
    }

    float gridColor = 1.0f;
    float mainRectangleColor = 1.0f;

    float4 originalColor = tex2D(inputSampler, uv);
    float4 colorAtMousePosition = tex2D(inputSampler, mousePosition);

    if (originalColor.r > 0.8 && originalColor.g > 0.8 && originalColor.b > 0.8)
    {
        gridColor = 0.0f;
    }

    if (colorAtMousePosition.r > 0.8 && colorAtMousePosition.g > 0.8 && colorAtMousePosition.b > 0.8)
    {
        mainRectangleColor = 0.0f;
    }

    float4 color = tex2D(inputSampler, uv);
    float distanceFromMouse = length(mousePosition - uv);
    float distanceFactor;

    int pixelPositionX = textureSize * uv.x;
    int pixelPositionY = textureSize * uv.y;

    int mousePositionX = mousePosition.x * textureSize;
    int mousePositionY = mousePosition.y * textureSize;

    int2 topLeftRectangle = int2(mousePositionX - (mousePositionX % squareSize) - 1, mousePositionY - (mousePositionY % squareSize) - 1);

    // do not draw grid inside square even when grid (avoid drawing grid in that area later
    if (((pixelPositionX >= topLeftRectangle.x + 1 && pixelPositionX <= topLeftRectangle.x + squareSize + 1) && (pixelPositionY == topLeftRectangle.y + 1 || pixelPositionY == topLeftRectangle.y + squareSize + 1)) ||
        ((pixelPositionY >= topLeftRectangle.y + 1 && pixelPositionY <= topLeftRectangle.y + squareSize + 1) && (pixelPositionX == topLeftRectangle.x + 1 || pixelPositionX == topLeftRectangle.x + squareSize + 1)))
    {
        return originalColor;
    }

    if (distanceFromMouse <= radius)
    {
        // Draw dot grid pattern - create circular dots at regular intervals
        int dotSpacing = squareSize;
        int dotRadius = max(1, squareSize / 8);
        
        int gridX = pixelPositionX % dotSpacing;
        int gridY = pixelPositionY % dotSpacing;
        
        // Calculate distance from the nearest grid point
        float distToGridX = min(gridX, dotSpacing - gridX);
        float distToGridY = min(gridY, dotSpacing - gridY);
        
        // Check if we're close to a grid intersection (for circular dots)
        if ((gridX <= dotRadius || gridX >= dotSpacing - dotRadius) && 
            (gridY <= dotRadius || gridY >= dotSpacing - dotRadius))
        {
            float dotDistanceX = gridX <= dotRadius ? gridX : dotSpacing - gridX;
            float dotDistanceY = gridY <= dotRadius ? gridY : dotSpacing - gridY;
            float dotDistance = sqrt(dotDistanceX * dotDistanceX + dotDistanceY * dotDistanceY);
            
            if (dotDistance <= dotRadius)
            {
                float dotIntensity = 1.0 - (dotDistance / dotRadius);
                if (gridColor == 1.0f)
                {
                    // For light backgrounds, make dots darker
                    color.r = color.r * (1.0 - dotIntensity * 0.6);
                    color.g = color.g * (1.0 - dotIntensity * 0.6);
                    color.b = color.b * (1.0 - dotIntensity * 0.6);
                }
                else
                {
                    // For dark backgrounds, make dots lighter
                    color.r = color.r + ((1.0 - color.r) * dotIntensity * 0.6);
                    color.g = color.g + ((1.0 - color.g) * dotIntensity * 0.6);
                    color.b = color.b + ((1.0 - color.b) * dotIntensity * 0.6);
                }
            }
        }
    }

    // Make the selection rectangle more prominent
    if (((pixelPositionX >= topLeftRectangle.x && pixelPositionX <= topLeftRectangle.x + squareSize + 2) && (pixelPositionY == topLeftRectangle.y || pixelPositionY == topLeftRectangle.y + squareSize + 2)) ||
        ((pixelPositionY >= topLeftRectangle.y && pixelPositionY <= topLeftRectangle.y + squareSize + 2) && (pixelPositionX == topLeftRectangle.x || pixelPositionX == topLeftRectangle.x + squareSize + 2)))
    {
        originalColor.r = mainRectangleColor;
        originalColor.g = mainRectangleColor;
        originalColor.b = mainRectangleColor;
        return originalColor;
    }

    return color;
}