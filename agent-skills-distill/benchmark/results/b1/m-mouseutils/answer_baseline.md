CULPRIT_FILES: src/modules/MouseUtils/MouseHighlighter/MouseHighlighter.cpp
CULPRIT_FUNCTIONS: AddDrawingPoint(), StartDrawingPointFading()
FIX: The circles are created with a fixed radius and only fade in opacity. Add a scale animation to create a ripple effect by animating the circle's Scale property from 0 to 1 or from 1 to a larger value during the fade animation, making the highlight grow outward like a ripple.
CITED_FIX_PR: none
CONFIDENCE: high
