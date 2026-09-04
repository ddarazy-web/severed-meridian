-- Servered Meridian 1단계 남성 주인공 4방향 대기/걷기 도트 원본 생성기.
-- Aseprite 배치 모드에서 실행하며 마스터 32색 팔레트 안의 색만 사용한다.

local frameSize = 128
local pivotX = 64
local pivotY = 116

local scriptDir = app.fs.filePath(app.scriptPath)
local repoRoot = app.params["repo_root"] or app.fs.joinPath(scriptDir, "..", "..")
local sourceDir = app.fs.joinPath(repoRoot, "ArtSource", "Production", "Characters")
local unityDir = app.fs.joinPath(repoRoot, "Assets", "Art", "Production", "Characters")
local previewDir = app.fs.joinPath(sourceDir, "Preview")

local colors = {
  paperLight = { 242, 230, 201, 255 },
  paperMid = { 216, 199, 165, 255 },
  inkCore = { 33, 31, 27, 255 },
  inkDeep = { 58, 55, 48, 255 },
  inkMid = { 90, 85, 74, 255 },
  inkLight = { 125, 117, 101, 255 },
  skinLight = { 240, 201, 161, 255 },
  skinMid = { 216, 155, 115, 255 },
  skinShade = { 173, 110, 86, 255 },
  woodInk = { 74, 48, 40, 255 },
  woodDeep = { 113, 70, 53, 255 },
  woodMid = { 154, 104, 70, 255 },
  woodLight = { 185, 138, 91, 255 },
  foliageInk = { 38, 63, 50, 255 },
  foliageDeep = { 62, 98, 67, 255 },
  bamboo = { 101, 128, 90, 255 },
  ironInkBlue = { 53, 65, 74, 255 },
  ironMid = { 89, 102, 106, 255 },
  ironLight = { 137, 145, 139, 255 },
  silverLight = { 194, 196, 182, 255 },
  vermilion = { 163, 59, 43, 255 },
  blueGray = { 78, 113, 128, 255 },
  shadow = { 33, 31, 27, 82 },
}

local function px(c)
  return app.pixelColor.rgba(c[1], c[2], c[3], c[4])
end

local function put(image, x, y, color)
  x = math.floor(x)
  y = math.floor(y)
  if x >= 0 and x < image.width and y >= 0 and y < image.height then
    image:drawPixel(x, y, px(color))
  end
end

local function rect(image, x, y, w, h, color)
  for yy = math.floor(y), math.floor(y + h - 1) do
    for xx = math.floor(x), math.floor(x + w - 1) do
      put(image, xx, yy, color)
    end
  end
end

local function ellipse(image, cx, cy, rx, ry, color)
  local left = math.floor(cx - rx)
  local right = math.ceil(cx + rx)
  local top = math.floor(cy - ry)
  local bottom = math.ceil(cy + ry)
  for y = top, bottom do
    for x = left, right do
      local nx = (x - cx) / rx
      local ny = (y - cy) / ry
      if nx * nx + ny * ny <= 1 then
        put(image, x, y, color)
      end
    end
  end
end

local function polygon(image, points, color)
  local minY = points[1][2]
  local maxY = points[1][2]
  for i = 2, #points do
    minY = math.min(minY, points[i][2])
    maxY = math.max(maxY, points[i][2])
  end
  for y = math.floor(minY), math.ceil(maxY) do
    local intersections = {}
    local scanY = y + 0.5
    local j = #points
    for i = 1, #points do
      local ax, ay = points[j][1], points[j][2]
      local bx, by = points[i][1], points[i][2]
      if (ay <= scanY and by > scanY) or (by <= scanY and ay > scanY) then
        local x = ax + (scanY - ay) * (bx - ax) / (by - ay)
        table.insert(intersections, x)
      end
      j = i
    end
    table.sort(intersections)
    for i = 1, #intersections, 2 do
      local x1 = math.ceil(intersections[i])
      local x2 = math.floor(intersections[i + 1] or intersections[i])
      for x = x1, x2 do
        put(image, x, y, color)
      end
    end
  end
end

local function line(image, x0, y0, x1, y1, color, thickness)
  x0, y0, x1, y1 = math.floor(x0), math.floor(y0), math.floor(x1), math.floor(y1)
  local dx = math.abs(x1 - x0)
  local sx = x0 < x1 and 1 or -1
  local dy = -math.abs(y1 - y0)
  local sy = y0 < y1 and 1 or -1
  local err = dx + dy
  thickness = thickness or 1
  while true do
    rect(image, x0 - math.floor(thickness / 2), y0 - math.floor(thickness / 2), thickness, thickness, color)
    if x0 == x1 and y0 == y1 then break end
    local e2 = 2 * err
    if e2 >= dy then err = err + dy; x0 = x0 + sx end
    if e2 <= dx then err = err + dx; y0 = y0 + sy end
  end
end

local function outlinedEllipse(image, cx, cy, rx, ry, outline, fill)
  ellipse(image, cx, cy, rx, ry, outline)
  ellipse(image, cx, cy + 1, rx - 1, ry - 1, fill)
end

local function outlinedRect(image, x, y, w, h, outline, fill)
  rect(image, x, y, w, h, outline)
  if w > 2 and h > 2 then rect(image, x + 1, y + 1, w - 2, h - 2, fill) end
end

local function newLayerImage()
  return Image(frameSize, frameSize, ColorMode.RGB)
end

local walkMotion = {
  { stride = 0, bob = 0 },
  { stride = 2, bob = -1 },
  { stride = 4, bob = 0 },
  { stride = 0, bob = 1 },
  { stride = -2, bob = 0 },
  { stride = -4, bob = -1 },
}

local function motionFor(action, phase)
  if action == "idle" then
    return { stride = 0, bob = 0, breath = phase == 2 and 1 or 0 }
  end
  local m = walkMotion[phase]
  return { stride = m.stride, bob = m.bob, breath = 0 }
end

local function drawHerbSprig(image, x, y, lean, motion)
  local sway = motion or 0
  line(image, x, y + 7, x + lean + sway, y - 1, colors.inkDeep, 2)
  polygon(image, { {x + lean + sway, y + 1}, {x + lean - 4 + sway, y - 2}, {x + lean - 2 + sway, y + 3} }, colors.foliageDeep)
  polygon(image, { {x + lean + sway, y + 1}, {x + lean + 4 + sway, y - 3}, {x + lean + 3 + sway, y + 3} }, colors.bamboo)
  polygon(image, { {x + math.floor(lean / 2), y + 4}, {x - 3, y + 2}, {x, y + 6} }, colors.foliageDeep)
end

local function drawBasketBack(image, direction, bob, sway)
  if direction == "up" then
    drawHerbSprig(image, 51, 47 + bob, -2, sway)
    drawHerbSprig(image, 62, 44 + bob, 1, -sway)
    drawHerbSprig(image, 75, 47 + bob, 3, sway)
    polygon(image, { {44, 53 + bob}, {84, 53 + bob}, {79, 83 + bob}, {49, 83 + bob} }, colors.woodInk)
    polygon(image, { {47, 55 + bob}, {81, 55 + bob}, {76, 80 + bob}, {52, 80 + bob} }, colors.woodMid)
    for y = 59 + bob, 77 + bob, 6 do line(image, 49, y, 79, y, colors.woodDeep, 2) end
    for x = 53, 75, 7 do line(image, x, 56 + bob, x - 2, 79 + bob, colors.woodMid, 1) end
  elseif direction == "down" then
    drawHerbSprig(image, 39, 50 + bob, -3, sway)
    drawHerbSprig(image, 88, 50 + bob, 3, -sway)
    polygon(image, { {32, 55 + bob}, {47, 53 + bob}, {45, 76 + bob}, {35, 78 + bob} }, colors.woodInk)
    polygon(image, { {34, 57 + bob}, {45, 56 + bob}, {43, 73 + bob}, {36, 75 + bob} }, colors.woodMid)
    polygon(image, { {81, 53 + bob}, {96, 55 + bob}, {93, 78 + bob}, {83, 76 + bob} }, colors.woodInk)
    polygon(image, { {83, 56 + bob}, {94, 57 + bob}, {91, 75 + bob}, {84, 73 + bob} }, colors.woodMid)
    line(image, 34, 62 + bob, 44, 61 + bob, colors.woodMid, 1)
    line(image, 84, 61 + bob, 94, 62 + bob, colors.woodMid, 1)
  elseif direction == "left" then
    drawHerbSprig(image, 79, 48 + bob, 4, sway)
    drawHerbSprig(image, 85, 53 + bob, 3, -sway)
    polygon(image, { {69, 52 + bob}, {88, 55 + bob}, {85, 82 + bob}, {69, 79 + bob} }, colors.woodInk)
    polygon(image, { {72, 55 + bob}, {85, 57 + bob}, {82, 78 + bob}, {72, 76 + bob} }, colors.woodMid)
    line(image, 72, 62 + bob, 84, 64 + bob, colors.woodMid, 1)
  else
    drawHerbSprig(image, 49, 48 + bob, -4, -sway)
    drawHerbSprig(image, 43, 53 + bob, -3, sway)
    polygon(image, { {40, 55 + bob}, {59, 52 + bob}, {59, 79 + bob}, {43, 82 + bob} }, colors.woodInk)
    polygon(image, { {43, 57 + bob}, {56, 55 + bob}, {56, 76 + bob}, {46, 78 + bob} }, colors.woodMid)
    line(image, 44, 64 + bob, 56, 62 + bob, colors.woodMid, 1)
  end
end

local function drawFaceAndHands(image, direction, bob, armSwing)
  local headY = 42 + bob
  if direction == "down" then
    outlinedEllipse(image, 64, headY, 21, 18, colors.inkCore, colors.skinLight)
    ellipse(image, 44, headY + 2, 4, 6, colors.inkCore)
    ellipse(image, 44, headY + 2, 2, 4, colors.skinMid)
    ellipse(image, 84, headY + 2, 4, 6, colors.inkCore)
    ellipse(image, 84, headY + 2, 2, 4, colors.skinMid)
    outlinedEllipse(image, 36, 76 + bob + armSwing, 4, 5, colors.inkCore, colors.skinMid)
    outlinedEllipse(image, 92, 76 + bob - armSwing, 4, 5, colors.inkCore, colors.skinMid)
  elseif direction == "up" then
    ellipse(image, 44, headY + 2, 4, 6, colors.inkCore)
    ellipse(image, 44, headY + 2, 2, 4, colors.skinMid)
    ellipse(image, 84, headY + 2, 4, 6, colors.inkCore)
    ellipse(image, 84, headY + 2, 2, 4, colors.skinMid)
    outlinedEllipse(image, 36, 76 + bob - armSwing, 4, 5, colors.inkCore, colors.skinMid)
    outlinedEllipse(image, 92, 76 + bob + armSwing, 4, 5, colors.inkCore, colors.skinMid)
  elseif direction == "left" then
    outlinedEllipse(image, 61, headY, 19, 18, colors.inkCore, colors.skinLight)
    rect(image, 40, headY - 1, 3, 4, colors.inkCore)
    rect(image, 39, headY, 3, 2, colors.skinMid)
    ellipse(image, 78, headY + 2, 4, 6, colors.inkCore)
    ellipse(image, 78, headY + 2, 2, 4, colors.skinMid)
    outlinedEllipse(image, 43 - armSwing, 77 + bob, 4, 5, colors.inkCore, colors.skinMid)
    outlinedEllipse(image, 84 + armSwing, 75 + bob, 4, 5, colors.inkCore, colors.skinMid)
  else
    outlinedEllipse(image, 67, headY, 19, 18, colors.inkCore, colors.skinLight)
    rect(image, 85, headY - 1, 3, 4, colors.inkCore)
    rect(image, 87, headY, 3, 2, colors.skinMid)
    ellipse(image, 50, headY + 2, 4, 6, colors.inkCore)
    ellipse(image, 50, headY + 2, 2, 4, colors.skinMid)
    outlinedEllipse(image, 44 - armSwing, 75 + bob, 4, 5, colors.inkCore, colors.skinMid)
    outlinedEllipse(image, 85 + armSwing, 77 + bob, 4, 5, colors.inkCore, colors.skinMid)
  end
end

local function drawLeg(image, x, y, footOffsetX, footOffsetY, patchSide)
  polygon(image, { {x - 7, y}, {x + 7, y}, {x + 6 + footOffsetX, y + 23 + footOffsetY}, {x - 7 + footOffsetX, y + 23 + footOffsetY} }, colors.inkCore)
  polygon(image, { {x - 5, y + 1}, {x + 5, y + 1}, {x + 4 + footOffsetX, y + 17 + footOffsetY}, {x - 5 + footOffsetX, y + 17 + footOffsetY} }, colors.inkMid)
  rect(image, x - 6 + footOffsetX, y + 15 + footOffsetY, 12, 5, colors.paperMid)
  line(image, x - 5 + footOffsetX, y + 16 + footOffsetY, x + 5 + footOffsetX, y + 19 + footOffsetY, colors.woodDeep, 1)
  line(image, x + 4 + footOffsetX, y + 16 + footOffsetY, x - 4 + footOffsetX, y + 19 + footOffsetY, colors.woodDeep, 1)
  polygon(image, { {x - 8 + footOffsetX, y + 20 + footOffsetY}, {x + 6 + footOffsetX, y + 20 + footOffsetY}, {x + 8 + footOffsetX, y + 25 + footOffsetY}, {x - 9 + footOffsetX, y + 25 + footOffsetY} }, colors.inkCore)
  rect(image, x - 6 + footOffsetX, y + 21 + footOffsetY, 12, 3, colors.ironInkBlue)
  if patchSide then rect(image, x - 4, y + 6, 6, 5, colors.inkMid) end
end

local function drawClothes(image, direction, bob, stride)
  local torsoY = 57 + bob
  local arm = math.floor(stride / 2)

  if direction == "left" or direction == "right" then
    local dir = direction == "left" and -1 or 1
    polygon(image, { {47, torsoY}, {78, torsoY - 1}, {84, torsoY + 28}, {45, torsoY + 28} }, colors.inkDeep)
    polygon(image, { {49, torsoY + 2}, {76, torsoY + 1}, {80, torsoY + 25}, {48, torsoY + 25} }, colors.ironMid)
    polygon(image, { {58, torsoY + 1}, {69, torsoY + 1}, {67, torsoY + 18}, {61, torsoY + 21}, {55, torsoY + 7} }, colors.paperMid)
    polygon(image, { {48, torsoY + 4}, {42 + arm, torsoY + 9}, {40 - arm, torsoY + 21}, {47 - arm, torsoY + 23}, {55, torsoY + 8} }, colors.inkDeep)
    polygon(image, { {49, torsoY + 6}, {44 + arm, torsoY + 10}, {43 - arm, torsoY + 19}, {48 - arm, torsoY + 20}, {55, torsoY + 9} }, colors.ironMid)
    polygon(image, { {76, torsoY + 5}, {83 - arm, torsoY + 10}, {87 + arm, torsoY + 20}, {81 + arm, torsoY + 23}, {72, torsoY + 10} }, colors.inkDeep)
    polygon(image, { {75, torsoY + 7}, {81 - arm, torsoY + 11}, {84 + arm, torsoY + 19}, {80 + arm, torsoY + 20}, {71, torsoY + 11} }, colors.ironMid)
    rect(image, 41 - arm, torsoY + 18, 8, 5, colors.paperMid)
    rect(image, 79 + arm, torsoY + 18, 8, 5, colors.paperMid)
    rect(image, 46, torsoY + 24, 36, 5, colors.inkCore)
    rect(image, 49, torsoY + 25, 30, 2, colors.woodDeep)
    drawLeg(image, 55, torsoY + 27, dir * math.max(stride, 0), math.max(-stride, 0) / 2, true)
    drawLeg(image, 72, torsoY + 27, -dir * math.max(-stride, 0), math.max(stride, 0) / 2, false)
  else
    polygon(image, { {43, torsoY}, {85, torsoY}, {90, torsoY + 29}, {38, torsoY + 29} }, colors.inkDeep)
    polygon(image, { {45, torsoY + 2}, {83, torsoY + 2}, {86, torsoY + 26}, {42, torsoY + 26} }, colors.ironMid)
    if direction == "down" then
      polygon(image, { {54, torsoY + 1}, {64, torsoY + 11}, {74, torsoY + 1}, {77, torsoY + 4}, {64, torsoY + 17}, {51, torsoY + 4} }, colors.paperMid)
      line(image, 64, torsoY + 13, 64, torsoY + 25, colors.ironInkBlue, 1)
    else
      rect(image, 54, torsoY + 2, 20, 5, colors.ironInkBlue)
      line(image, 64, torsoY + 7, 64, torsoY + 24, colors.ironInkBlue, 1)
    end
    polygon(image, { {44, torsoY + 4}, {36, torsoY + 9 + arm}, {33, torsoY + 21 + arm}, {41, torsoY + 24 + arm}, {50, torsoY + 9} }, colors.inkDeep)
    polygon(image, { {45, torsoY + 6}, {38, torsoY + 10 + arm}, {36, torsoY + 19 + arm}, {41, torsoY + 21 + arm}, {51, torsoY + 9} }, colors.ironMid)
    polygon(image, { {84, torsoY + 4}, {92, torsoY + 9 - arm}, {95, torsoY + 21 - arm}, {87, torsoY + 24 - arm}, {78, torsoY + 9} }, colors.inkDeep)
    polygon(image, { {83, torsoY + 6}, {90, torsoY + 10 - arm}, {92, torsoY + 19 - arm}, {87, torsoY + 21 - arm}, {77, torsoY + 9} }, colors.ironMid)
    rect(image, 34, torsoY + 19 + arm, 9, 5, colors.paperMid)
    rect(image, 85, torsoY + 19 - arm, 9, 5, colors.paperMid)
    rect(image, 40, torsoY + 25, 48, 5, colors.inkCore)
    rect(image, 43, torsoY + 26, 42, 2, colors.woodDeep)

    local leftForward = stride > 0
    local rightForward = stride < 0
    local leftX = 53 + (leftForward and -2 or 0)
    local rightX = 75 + (rightForward and 2 or 0)
    local leftFootY = leftForward and 1 or (rightForward and -1 or 0)
    local rightFootY = rightForward and 1 or (leftForward and -1 or 0)
    drawLeg(image, leftX, torsoY + 27, math.floor(stride / 2), leftFootY, true)
    drawLeg(image, rightX, torsoY + 27, math.floor(-stride / 2), rightFootY, false)
  end
end

local function drawHairAndFace(image, direction, bob, ribbonSway)
  local cy = 41 + bob
  if direction == "up" then
    outlinedEllipse(image, 64, cy, 22, 19, colors.inkDeep, colors.inkDeep)
    polygon(image, { {43, cy - 3}, {47, cy - 15}, {58, cy - 19}, {74, cy - 18}, {84, cy - 8}, {86, cy + 8}, {79, cy + 17}, {48, cy + 16}, {41, cy + 7} }, colors.inkDeep)
    polygon(image, { {48, cy - 10}, {54, cy - 16}, {58, cy - 4}, {55, cy + 5}, {50, cy + 1} }, colors.inkMid)
    polygon(image, { {58, cy - 17}, {64, cy - 19}, {66, cy - 6}, {63, cy + 5}, {59, cy - 1} }, colors.ironMid)
    polygon(image, { {69, cy - 18}, {77, cy - 13}, {76, cy - 2}, {70, cy + 7}, {68, cy - 4} }, colors.inkMid)
    rect(image, 51, cy + 8, 5, 3, colors.ironMid)
    rect(image, 72, cy + 7, 5, 3, colors.ironMid)
  elseif direction == "down" then
    polygon(image, { {43, cy - 2}, {46, cy - 15}, {56, cy - 20}, {73, cy - 19}, {84, cy - 10}, {86, cy + 2}, {80, cy - 2}, {76, cy + 7}, {70, cy + 1}, {65, cy + 9}, {59, cy + 1}, {53, cy + 8}, {47, cy + 2} }, colors.inkDeep)
    polygon(image, { {48, cy - 10}, {54, cy - 17}, {58, cy - 4}, {55, cy + 4}, {50, cy} }, colors.inkMid)
    polygon(image, { {56, cy - 18}, {63, cy - 20}, {64, cy - 5}, {60, cy + 3}, {58, cy - 5} }, colors.inkMid)
    polygon(image, { {66, cy - 19}, {72, cy - 18}, {73, cy - 6}, {69, cy + 5}, {66, cy - 3} }, colors.ironMid)
    polygon(image, { {75, cy - 15}, {81, cy - 9}, {79, cy + 1}, {74, cy + 5}, {73, cy - 5} }, colors.inkMid)
    line(image, 49, cy + 7, 58, cy + 6, colors.inkCore, 1)
    line(image, 70, cy + 6, 79, cy + 7, colors.inkCore, 1)
    outlinedRect(image, 51, cy + 9, 8, 7, colors.inkCore, colors.woodDeep)
    outlinedRect(image, 69, cy + 9, 8, 7, colors.inkCore, colors.woodDeep)
    rect(image, 53, cy + 10, 4, 3, colors.woodMid)
    rect(image, 71, cy + 10, 4, 3, colors.woodMid)
    put(image, 53, cy + 10, colors.paperMid)
    put(image, 73, cy + 10, colors.paperMid)
    rect(image, 63, cy + 15, 2, 2, colors.skinShade)
    line(image, 60, cy + 20, 68, cy + 20, colors.woodInk, 1)
  elseif direction == "left" then
    polygon(image, { {42, cy - 2}, {45, cy - 14}, {56, cy - 20}, {72, cy - 17}, {81, cy - 7}, {80, cy + 8}, {71, cy + 2}, {65, cy + 9}, {58, cy + 2}, {50, cy + 8}, {43, cy + 4} }, colors.inkDeep)
    polygon(image, { {47, cy - 10}, {54, cy - 18}, {58, cy - 3}, {54, cy + 5}, {49, cy} }, colors.inkMid)
    polygon(image, { {57, cy - 18}, {65, cy - 18}, {65, cy - 6}, {60, cy + 4}, {58, cy - 5} }, colors.inkMid)
    polygon(image, { {67, cy - 16}, {75, cy - 11}, {73, cy}, {67, cy + 7}, {66, cy - 3} }, colors.ironMid)
    line(image, 45, cy + 7, 54, cy + 6, colors.inkCore, 1)
    outlinedRect(image, 46, cy + 9, 8, 7, colors.inkCore, colors.woodDeep)
    rect(image, 48, cy + 10, 4, 3, colors.woodMid)
    put(image, 48, cy + 10, colors.paperMid)
    line(image, 43, cy + 20, 49, cy + 20, colors.woodInk, 1)
  else
    polygon(image, { {86, cy - 2}, {83, cy - 14}, {72, cy - 20}, {56, cy - 17}, {47, cy - 7}, {48, cy + 8}, {57, cy + 2}, {63, cy + 9}, {70, cy + 2}, {78, cy + 8}, {85, cy + 4} }, colors.inkDeep)
    polygon(image, { {81, cy - 10}, {74, cy - 18}, {70, cy - 3}, {74, cy + 5}, {79, cy} }, colors.inkMid)
    polygon(image, { {71, cy - 18}, {63, cy - 18}, {63, cy - 6}, {68, cy + 4}, {70, cy - 5} }, colors.inkMid)
    polygon(image, { {61, cy - 16}, {53, cy - 11}, {55, cy}, {61, cy + 7}, {62, cy - 3} }, colors.ironMid)
    line(image, 74, cy + 6, 83, cy + 7, colors.inkCore, 1)
    outlinedRect(image, 74, cy + 9, 8, 7, colors.inkCore, colors.woodDeep)
    rect(image, 76, cy + 10, 4, 3, colors.woodMid)
    put(image, 79, cy + 10, colors.paperMid)
    line(image, 79, cy + 20, 85, cy + 20, colors.woodInk, 1)
  end

  -- 상투와 방향에 따른 실제 매듭 위치. 좌우 시트는 소품 배치가 달라 단순 반전이 아니다.
  ellipse(image, 64, cy - 17, 10, 6, colors.inkDeep)
  ellipse(image, 64, cy - 17, 8, 5, colors.inkMid)
  rect(image, 59, cy - 21, 6, 2, colors.ironMid)
  rect(image, 55, cy - 14, 18, 2, colors.blueGray)
  local knotX = 76
  local tailDir = 1
  if direction == "up" then knotX = 52; tailDir = -1 end
  if direction == "left" then knotX = 78; tailDir = 1 end
  if direction == "right" then knotX = 50; tailDir = -1 end
  polygon(image, { {knotX, cy - 15}, {knotX + 8 * tailDir + ribbonSway, cy - 19}, {knotX + 7 * tailDir + ribbonSway, cy - 14}, {knotX + 11 * tailDir + ribbonSway, cy - 11}, {knotX + 2 * tailDir, cy - 12} }, colors.inkCore)
  polygon(image, { {knotX, cy - 14}, {knotX + 7 * tailDir + ribbonSway, cy - 17}, {knotX + 6 * tailDir + ribbonSway, cy - 14}, {knotX + 9 * tailDir + ribbonSway, cy - 12}, {knotX + 2 * tailDir, cy - 13} }, colors.blueGray)
end

local function drawFrontEquipment(image, direction, bob, stride)
  local torsoY = 57 + bob
  if direction == "down" then
    line(image, 46, torsoY + 1, 53, torsoY + 25, colors.woodDeep, 3)
    line(image, 82, torsoY + 1, 75, torsoY + 25, colors.woodDeep, 3)
    outlinedRect(image, 34, torsoY + 21, 16, 21, colors.woodInk, colors.woodDeep)
    rect(image, 37, torsoY + 24, 10, 3, colors.woodMid)
    polygon(image, { {43, torsoY + 35}, {47, torsoY + 35}, {47, torsoY + 41}, {44, torsoY + 39} }, colors.paperMid)
    line(image, 39, torsoY + 31, 39, torsoY + 39, colors.woodMid, 1)
    line(image, 44, torsoY + 31, 44, torsoY + 39, colors.woodMid, 1)
    polygon(image, { {80, torsoY + 20}, {88, torsoY + 17}, {91, torsoY + 36}, {82, torsoY + 38} }, colors.woodInk)
    polygon(image, { {82, torsoY + 21}, {86, torsoY + 20}, {88, torsoY + 34}, {84, torsoY + 35} }, colors.woodDeep)
    rect(image, 80, torsoY + 21, 8, 4, colors.vermilion)
    rect(image, 81, torsoY + 27, 8, 3, colors.vermilion)
  elseif direction == "up" then
    line(image, 49, torsoY + 1, 55, torsoY + 25, colors.woodDeep, 3)
    line(image, 79, torsoY + 1, 73, torsoY + 25, colors.woodDeep, 3)
    outlinedRect(image, 78, torsoY + 23, 12, 17, colors.woodInk, colors.woodDeep)
    polygon(image, { {38, torsoY + 22}, {45, torsoY + 19}, {47, torsoY + 34}, {40, torsoY + 36} }, colors.woodInk)
    rect(image, 40, torsoY + 23, 7, 4, colors.vermilion)
  elseif direction == "left" then
    line(image, 71, torsoY + 1, 67, torsoY + 24, colors.woodDeep, 3)
    outlinedRect(image, 68, torsoY + 20, 15, 20, colors.woodInk, colors.woodDeep)
    rect(image, 71, torsoY + 23, 9, 3, colors.woodMid)
    polygon(image, { {45, torsoY + 22}, {51, torsoY + 19}, {53, torsoY + 34}, {47, torsoY + 36} }, colors.woodInk)
    rect(image, 47, torsoY + 23, 7, 4, colors.vermilion)
  else
    line(image, 57, torsoY + 1, 61, torsoY + 24, colors.woodDeep, 3)
    outlinedRect(image, 45, torsoY + 20, 15, 20, colors.woodInk, colors.woodDeep)
    rect(image, 48, torsoY + 23, 9, 3, colors.woodMid)
    polygon(image, { {77, torsoY + 19}, {83, torsoY + 22}, {81, torsoY + 36}, {75, torsoY + 34} }, colors.woodInk)
    rect(image, 75, torsoY + 23, 7, 4, colors.vermilion)
  end
end

local function renderFrame(action, direction, phase)
  local m = motionFor(action, phase)
  local upperBob = m.bob + m.breath
  local sway = 0
  if action == "walk" then
    sway = m.stride > 0 and 1 or (m.stride < 0 and -1 or 0)
  end
  local layers = {
    shadow = newLayerImage(),
    equipment_back = newLayerImage(),
    body = newLayerImage(),
    clothes = newLayerImage(),
    head_hair = newLayerImage(),
    equipment_front = newLayerImage(),
  }
  ellipse(layers.shadow, pivotX, 115, action == "walk" and 22 or 20, action == "walk" and 4 or 3, colors.shadow)
  drawBasketBack(layers.equipment_back, direction, upperBob, sway)
  drawFaceAndHands(layers.body, direction, upperBob, math.floor(m.stride / 2))
  drawClothes(layers.clothes, direction, m.bob, m.stride)
  drawHairAndFace(layers.head_hair, direction, upperBob, sway)
  drawFrontEquipment(layers.equipment_front, direction, m.bob, m.stride)
  return layers
end

local sprite = Sprite(frameSize, frameSize, ColorMode.RGB)
sprite.filename = app.fs.joinPath(sourceDir, "chr_protagonist_male_stage_01.aseprite")
sprite.layers[1].name = "shadow"
local layerNames = { "shadow", "equipment_back", "body", "clothes", "head_hair", "equipment_front" }
local layersByName = { shadow = sprite.layers[1] }
for i = 2, #layerNames do
  local layer = sprite:newLayer()
  layer.name = layerNames[i]
  layersByName[layerNames[i]] = layer
end

local paletteEntries = {
  colors.paperMid, colors.inkCore, colors.inkDeep, colors.inkMid,
  colors.skinLight, colors.skinMid, colors.skinShade, colors.woodInk, colors.woodDeep,
  colors.woodMid, colors.foliageDeep, colors.bamboo, colors.ironInkBlue, colors.ironMid,
  colors.vermilion, colors.blueGray,
}
local palette = Palette(#paletteEntries)
for i = 1, #paletteEntries do
  local c = paletteEntries[i]
  palette:setColor(i - 1, Color { r = c[1], g = c[2], b = c[3], a = c[4] })
end
sprite:setPalette(palette)

local directions = { "down", "left", "right", "up" }
local definitions = {}
for _, action in ipairs({ "idle", "walk" }) do
  local count = action == "idle" and 2 or 6
  for _, direction in ipairs(directions) do
    local first = #definitions + 1
    for phase = 1, count do
      table.insert(definitions, { action = action, direction = direction, phase = phase })
    end
    local last = #definitions
    definitions[first].tagStart = first
    definitions[first].tagEnd = last
    definitions[first].tagName = action .. "_" .. direction
  end
end

for i = 2, #definitions do sprite:newEmptyFrame() end

local rendered = {}
app.transaction("1단계 남성 주인공 애니메이션 생성", function()
  for frameIndex, def in ipairs(definitions) do
    local layerImages = renderFrame(def.action, def.direction, def.phase)
    rendered[frameIndex] = layerImages
    for _, layerName in ipairs(layerNames) do
      sprite:newCel(layersByName[layerName], sprite.frames[frameIndex], layerImages[layerName], Point(0, 0))
    end
    sprite.frames[frameIndex].duration = def.action == "idle" and 0.25 or 0.125
    if def.tagName then
      local tag = sprite:newTag(sprite.frames[def.tagStart], sprite.frames[def.tagEnd])
      tag.name = def.tagName
      tag.aniDir = AniDir.FORWARD
    end
  end
end)

local function flattened(frameIndex)
  local result = Image(frameSize, frameSize, ColorMode.RGB)
  for _, layerName in ipairs(layerNames) do
    result:drawImage(rendered[frameIndex][layerName], Point(0, 0))
  end
  return result
end

local function saveActionSheet(action, columns, firstFrame, outputPath)
  local sheet = Image(frameSize * columns, frameSize * 4, ColorMode.RGB)
  for row = 0, 3 do
    for col = 0, columns - 1 do
      local frameIndex = firstFrame + row * columns + col
      sheet:drawImage(flattened(frameIndex), Point(col * frameSize, row * frameSize))
    end
  end
  sheet:saveAs(outputPath)
end

local function scaleNearest(source, scale)
  local result = Image(source.width * scale, source.height * scale, ColorMode.RGB)
  for y = 0, source.height - 1 do
    for x = 0, source.width - 1 do
      local pixel = source:getPixel(x, y)
      for yy = 0, scale - 1 do
        for xx = 0, scale - 1 do
          result:drawPixel(x * scale + xx, y * scale + yy, pixel)
        end
      end
    end
  end
  return result
end

local function savePreview(action, columns, firstFrame, outputPath)
  local previewScale = 2
  local preview = Sprite(frameSize * 4 * previewScale, frameSize * previewScale, ColorMode.RGB)
  for i = 2, columns do preview:newEmptyFrame() end
  for col = 0, columns - 1 do
    local composed = Image(frameSize * 4, frameSize, ColorMode.RGB)
    rect(composed, 0, 0, frameSize * 4, frameSize, colors.paperLight)
    for row = 0, 3 do
      local frameIndex = firstFrame + row * columns + col
      composed:drawImage(flattened(frameIndex), Point(row * frameSize, 0))
    end
    preview:newCel(preview.layers[1], preview.frames[col + 1], scaleNearest(composed, previewScale), Point(0, 0))
    preview.frames[col + 1].duration = action == "idle" and 0.25 or 0.125
  end
  preview:saveAs(outputPath)
  preview:close()
end

sprite:saveAs(sprite.filename)
saveActionSheet("idle", 2, 1, app.fs.joinPath(unityDir, "chr_protagonist_male_stage_01_idle.png"))
saveActionSheet("walk", 6, 9, app.fs.joinPath(unityDir, "chr_protagonist_male_stage_01_walk.png"))
savePreview("idle", 2, 1, app.fs.joinPath(previewDir, "chr_protagonist_male_stage_01_idle_preview.gif"))
savePreview("walk", 6, 9, app.fs.joinPath(previewDir, "chr_protagonist_male_stage_01_walk_preview.gif"))

print("Generated: " .. sprite.filename)
sprite:close()
