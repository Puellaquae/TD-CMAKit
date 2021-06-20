using SkiaSharp;
using System;
using System.Collections.Generic;
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
            FillLayout(node, nodeLayout, bitmap);
            SaveImage(imgFilePath, bitmap);
        }

        private static void SaveImage(string imgFilePath, SKBitmap bitmap)
        {
            SKImage img = SKImage.FromBitmap(bitmap);
            SKData data = img.Encode(SKEncodedImageFormat.Png, 100);
            using FileStream stream = new(imgFilePath, FileMode.Create);
            data.SaveTo(stream);
        }

        private static void FillLayout(CodeNode node, Dictionary<CodeNode, (int x, int y)> nodeLayout, SKBitmap bitmap)
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

                SKPaint whitePaint = new()
                {
                    Color = SKColors.White,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 4
                };
                canvas.DrawRect(x - BoxWidth / 2, y - BoxHeight / 2, BoxWidth, BoxHeight, blackPaint);

                SKPaint fontPaint = new()
                {
                    Color = SKColors.Black,
                    TextSize = 48,
                    Typeface = SKTypeface.FromFamilyName("黑体"),
                    IsAntialias = true
                };

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
                    canvas.DrawText(code, x - size1.Width / 2, y - halfh + size1.Height, fontPaint);
                    canvas.DrawText("PC+1", x - size2.Width / 2, y + halfh, fontPaint);
                }
                else
                {
                    SKRect size = new();
                    fontPaint.MeasureText(code, ref size);
                    canvas.DrawText(code, x - size.Width / 2, y + size.Height / 2, fontPaint);
                }

                if (codeNode.NotProcessNext)
                {
                    continue;
                }

                foreach (CodeNode nextNode in codeNode.NextNodes)
                {
                    (int nx, int ny) = nodeLayout[nextNode];
                    int dx = nx - x;
                    int dy = ny - y;
                    dy -= BoxHeight / 2;
                    canvas.DrawLine(x, y + BoxHeight / 2, x, y + dy - ArrowHeight, blackPaint);
                    canvas.DrawLine(x + dx, y + dy - ArrowHeight, x, y + dy - ArrowHeight, blackPaint);
                    canvas.DrawLine(x + dx, y + dy - ArrowHeight, x + dx, y + dy, blackPaint);
                }

                if (codeNode.HasTest != 0)
                {
                    float cx = x;
                    float cy = y + BoxHeight + ArrowHeight;
                    float xl = cx - BoxWidth / 2;
                    float yb = cy - BoxHeight / 2;
                    float xr = cx + BoxWidth / 2;
                    float yt = cy + BoxHeight / 2;
                    canvas.DrawLine(xl, cy, cx, yb, blackPaint);
                    canvas.DrawLine(cx, yb, xr, cy, blackPaint);
                    canvas.DrawLine(xr, cy, cx, yt, blackPaint);
                    canvas.DrawLine(cx, yt, xl, cy, blackPaint);
                    canvas.DrawLine(cx, yt, cx, yb, whitePaint);

                    SKRect size = new SKRect();
                    fontPaint.MeasureText($"P{codeNode.HasTest}", ref size);
                    canvas.DrawText($"P{codeNode.HasTest}", x - size.Width / 2, y + ArrowHeight + BoxHeight + size.Height / 2, fontPaint);
                }
            }
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
