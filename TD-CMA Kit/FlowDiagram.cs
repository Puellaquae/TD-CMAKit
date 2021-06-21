using SkiaSharp;
using SkiaSharp.HarfBuzz;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TD_CMAKit.MicrocodeCompiler;


namespace TD_CMAKit
{
    public class FlowDiagram
    {
        private static readonly int BoxWidth = 300;
        private static readonly int BoxHeight = 150;
        private static readonly int ArrowHeight = 75;
        private static readonly int BlankWidth = 75;
        private static readonly int Padding = 200;
        public static void Draw(CodeNode node, string imgFilePath)
        {
            Dictionary<CodeNode, (int x, int y)> nodeLayout = new();
            (int width, int height) = BuildLayout(node, nodeLayout);
            SKBitmap bitmap = new(new SKImageInfo(width, height));
            FillLayout(nodeLayout, bitmap);
            SaveImage(imgFilePath, bitmap);
        }

        private static void SaveImage(string imgFilePath, SKBitmap bitmap)
        {
            SKImage img = SKImage.FromBitmap(bitmap);
            SKData data = img.Encode(SKEncodedImageFormat.Png, 100);
            using FileStream stream = new(imgFilePath, FileMode.Create);
            data.SaveTo(stream);
        }

        private static void FillLayout(Dictionary<CodeNode, (int x, int y)> nodeLayout, SKBitmap bitmap)
        {
            using SKCanvas canvas = new(bitmap);
            canvas.DrawColor(SKColors.White);

            foreach (var (codeNode, (x, y)) in nodeLayout)
            {
                SKPaint blackPaint = new()
                {
                    Color = SKColors.Black,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 4
                };

                canvas.DrawRect(x - BoxWidth / 2, y - BoxHeight / 2, BoxWidth, BoxHeight, blackPaint);

                SKPaint fontPaint = new()
                {
                    Color = SKColors.Black,
                    TextSize = 48,
                    Typeface = SKTypeface.FromFamilyName("Fira Code"),
                    IsAntialias = true
                };

                DrawCode(codeNode, x, y, canvas, fontPaint);
                DrawPlace(codeNode, x, y, canvas, fontPaint);
                DrawLabel(codeNode, x, y, canvas, fontPaint);

                if (codeNode.NotProcessNext)
                {
                    DrawEnd(codeNode, x, y, canvas, blackPaint, fontPaint);
                    continue;
                }

                foreach (CodeNode nextNode in codeNode.NextNodes)
                {
                    (int nx, int ny) = nodeLayout[nextNode];
                    DrawArrow(x, y, nx, ny, canvas, blackPaint);
                }

                DrawTestDiamond(codeNode, x, y, canvas, blackPaint, fontPaint);
            }
        }

        private static void DrawArrow(int x, int y, int nx, int ny, SKCanvas canvas, SKPaint paint)
        {
            int dx = nx - x;
            int dy = ny - y;
            dy -= BoxHeight / 2;

            canvas.DrawLine(x, y + BoxHeight / 2, x, y + dy - ArrowHeight, paint);
            canvas.DrawLine(x + dx, y + dy - ArrowHeight, x, y + dy - ArrowHeight, paint);
            canvas.DrawLine(x + dx, y + dy - ArrowHeight, x + dx, y + dy, paint);
        }

        private static void DrawTestDiamond(CodeNode codeNode, int x, int y, SKCanvas canvas, SKPaint paint, SKPaint fontPaint)
        {
            SKPaint whitePaint = new()
            {
                Color = SKColors.White,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 4
            };

            if (codeNode.HasTest == 0) return;

            float cx = x;
            float cy = y + BoxHeight + ArrowHeight;
            float xl = cx - BoxWidth / 2;
            float yb = cy - BoxHeight / 2;
            float xr = cx + BoxWidth / 2;
            float yt = cy + BoxHeight / 2;
            canvas.DrawLine(cx, yt, cx, yb, whitePaint);
            canvas.DrawLine(xl, cy, cx, yb, paint);
            canvas.DrawLine(cx, yb, xr, cy, paint);
            canvas.DrawLine(xr, cy, cx, yt, paint);
            canvas.DrawLine(cx, yt, xl, cy, paint);

            SKRect size = new();
            fontPaint.MeasureText($"P{codeNode.HasTest}", ref size);
            canvas.DrawText($"P{codeNode.HasTest}", x - size.Width / 2, y + ArrowHeight + BoxHeight + size.Height / 2,
                fontPaint);
        }

        private static void DrawCode(CodeNode codeNode, int x, int y, SKCanvas canvas, SKPaint fontPaint)
        {
            string[] tmp = codeNode.Code.Split('=');
            string code;
            bool twoLine = false;

            if (tmp.Length == 1)
            {
                code = codeNode.Code;
            }
            else
            {
                if (tmp[1] == "PC++")
                {
                    code = "PC->" + tmp[0];
                    twoLine = true;
                }
                else
                {
                    code = tmp[1] + "->" + tmp[0];
                }
            }

            if (twoLine)
            {
                SKRect size1 = new();
                fontPaint.MeasureText(code, ref size1);
                SKRect size2 = new();
                fontPaint.MeasureText("PC+1", ref size2);
                float halfh = (size1.Height + 20 + size2.Height) / 2;
                canvas.DrawShapedText(code, x - size1.Width / 2, y - halfh + size1.Height, fontPaint);
                canvas.DrawShapedText("PC+1", x - size2.Width / 2, y + halfh, fontPaint);
            }
            else
            {
                SKRect size = new();
                fontPaint.MeasureText(code, ref size);
                canvas.DrawShapedText(code, x - size.Width / 2, y + size.Height / 2, fontPaint);
            }
        }

        private static void DrawPlace(CodeNode codeNode, int x, int y, SKCanvas canvas, SKPaint fontPaint)
        {
            SKRect size = new();
            string place = $"{codeNode.PlaceInReal:X2}";
            fontPaint.MeasureText(place, ref size);
            canvas.DrawShapedText(place, x + BoxWidth / 2 - size.Width, y - BoxHeight / 2 - 10,
                fontPaint);
        }

        private static void DrawLabel(CodeNode codeNode, int x, int y, SKCanvas canvas, SKPaint fontPaint)
        {
            if (codeNode.StartOfLabel is null)
            {
                return;
            }

            string label = codeNode.StartOfLabel;

            if (!label.EndsWith('#'))
            {
                return;
            }

            label = label.Trim('!', '#');
            SKRect size = new();
            fontPaint.MeasureText(label, ref size);

            float tx = x - BoxWidth / 2;

            if (size.Width + 5 > BoxWidth / 2)
            {
                tx = x - size.Width - 5;
            }

            float ty = y - BoxHeight / 2 - 10;

            canvas.DrawShapedText(label, tx, ty, fontPaint);
        }

        private static void DrawEnd(CodeNode codeNode, int x, int y, SKCanvas canvas, SKPaint paint, SKPaint fontPaint)
        {
            int nextPlace = codeNode.NextNodes[0].PlaceInReal;
            string place = $"{nextPlace:X2}";

            float cy = y + BoxHeight / 2 + ArrowHeight;

            SKPoint pl = new(x - 50, cy);
            SKPoint pll = new(pl.X - 25, pl.Y + 25);
            SKPoint pr = new(x + 50, cy);
            SKPoint prr = new(pr.X + 25, pr.Y - 25);

            canvas.DrawLine(x, y + BoxHeight / 2, x, cy, paint);

            canvas.DrawLine(pll, pl, paint);
            canvas.DrawLine(pl, pr, paint);
            canvas.DrawLine(pr, prr, paint);

            SKRect size = new();
            fontPaint.MeasureText(place, ref size);

            canvas.DrawShapedText(place, x - size.Width / 2, cy + 10 + size.Height, fontPaint);
        }

        private static (int width, int height) BuildLayout(CodeNode node, Dictionary<CodeNode, (int x, int y)> nodeLayout)
        {
            Dictionary<CodeNode, int> multiInNodeLeftX = new();
            Dictionary<CodeNode, int> nodeInDegree = GetCodeGraphInDegree(node);
            Queue<CodeNode> queue = new();
            queue.Enqueue(node);
            nodeLayout.Add(node, (0, 0));
            while (queue.Count != 0)
            {
                CodeNode cNode = queue.Dequeue();
                if (cNode.NotProcessNext)
                {
                    continue;
                }

                CodeNode[] nodes = cNode.NextNodes.ToArray();
                (int x, int y) = nodeLayout[cNode];

                if (nodes.Length == 1)
                {
                    int cx = x;
                    int cy = y - (BoxHeight + ArrowHeight);

                    if (nodeLayout.ContainsKey(nodes[0]))
                    {
                        (int tx, int ty) = nodeLayout[nodes[0]];

                        if (multiInNodeLeftX.ContainsKey(nodes[0]))
                        {
                            tx = multiInNodeLeftX[nodes[0]];
                        }
                        else
                        {
                            multiInNodeLeftX.Add(nodes[0], tx);
                        }

                        int newX = (tx + cx) / 2;
                        int newY = Math.Min(cy - ArrowHeight, ty);
                        nodeLayout[nodes[0]] = (newX, newY);
                    }
                    else
                    {
                        nodeLayout.Add(nodes[0], (cx, cy));
                    }

                    nodeInDegree[nodes[0]]--;
                    if (nodeInDegree[nodes[0]] == 0)
                    {
                        queue.Enqueue(nodes[0]);
                    }
                }
                else
                {
                    int width = (nodes.Length - 1) * (BoxWidth + BlankWidth);
                    int xLeft = x - width / 2;
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        int cx = xLeft + (BlankWidth + BoxWidth) * i;
                        int cy = y - (2 * BoxHeight + 3 * ArrowHeight);

                        if (nodeLayout.ContainsKey(nodes[i]))
                        {
                            (int tx, int ty) = nodeLayout[nodes[i]];

                            if (multiInNodeLeftX.ContainsKey(nodes[i]))
                            {
                                tx = multiInNodeLeftX[nodes[i]];
                            }
                            else
                            {
                                multiInNodeLeftX.Add(nodes[i], tx);
                            }

                            int newX = (tx + cx) / 2;
                            int newY = Math.Min(cy - ArrowHeight, ty);
                            nodeLayout[nodes[i]] = (newX, newY);
                        }
                        else
                        {
                            nodeLayout.Add(nodes[i], (cx, cy));
                        }

                        nodeInDegree[nodes[i]]--;
                        if (nodeInDegree[nodes[i]] == 0)
                        {
                            queue.Enqueue(nodes[i]);
                        }
                    }
                }
            }

            int left = 0, right = 0, top = 0, bottom = 0;

            foreach (var (_, (x, y)) in nodeLayout)
            {
                left = Math.Min(x, left);
                right = Math.Max(x, right);
                top = Math.Max(y, top);
                bottom = Math.Min(y, bottom);
            }

            int layoutWidth = 2 * Padding + BoxWidth + right - left;
            int layoutHeight = 2 * Padding + BoxHeight + top - bottom;

            foreach (var (codeNode, (x, y)) in nodeLayout)
            {
                nodeLayout[codeNode] = (x - left + BoxWidth / 2 + Padding, top - y + BoxHeight / 2 + Padding);
            }

            return (layoutWidth, layoutHeight);
        }

        private static Dictionary<CodeNode, int> GetCodeGraphInDegree(CodeNode node)
        {
            Stack<CodeNode> stack = new();
            stack.Push(node);
            Dictionary<CodeNode, HashSet<CodeNode>> inDegree = new();
            while (stack.Count != 0)
            {
                CodeNode cNode = stack.Pop();
                if (cNode.NotProcessNext)
                {
                    continue;
                }

                foreach (CodeNode nextNode in cNode.NextNodes)
                {
                    if (!inDegree.ContainsKey(nextNode))
                    {
                        inDegree.Add(nextNode, new HashSet<CodeNode>());
                    }
                    inDegree[nextNode].Add(cNode);
                    stack.Push(nextNode);
                }
            }

            Dictionary<CodeNode, int> inD = new();
            foreach (var (key, value) in inDegree)
            {
                inD.Add(key, value.Count);
            }

            return inD;
        }
    }
}
