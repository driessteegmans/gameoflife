/*
 * AIMIS / LIFE
 * Copyright (C) 2014, 2015 Alexis Enston
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 */

using System;
using System.Diagnostics;
using System.Drawing;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System.Drawing.Imaging;

namespace LifeGame
{
    public class Game
    {

#if DEBUG
        // Debug stopwatches to measure execution time while debugging.
        private readonly Stopwatch _frameRenderStopwatch = new Stopwatch();
        private readonly Stopwatch _simulationStopwatch = new Stopwatch();
#endif

        //draw a circle on the opentk gamewindow
        public void DrawCircle(int segments, float xpos, float ypos, float radius)
        {
            GL.Begin(PrimitiveType.Polygon);
            for (int i = 0; i < segments; i++)
            {
                float theta = (2.0f * (float)Math.PI * i) / (float)segments;
                float cxx = radius * (float)Math.Cos(theta);
                float cyy = radius * (float)Math.Sin(theta);
                GL.Vertex2(xpos + cxx, ypos + cyy);
            }
            GL.End();
        }


        //get the position of the mouse as a vector scaled to the gamewindow
        public Vector2 MousePosition(float mX, float mY, GameWindow game)
        {
            Vector2 vecMousePos = new Vector2(((mX) / (float)game.Width - 0.5f) * game.Width * 2 *
                                              ZoomMulti - ViewPointV.X * game.Width * ZoomMulti,
                0 - ((mY) / (float)game.Height - 0.5f)
                * game.Height * 2 * ZoomMulti - ViewPointV.Y * game.Height * ZoomMulti);
            return vecMousePos;
        }

        //For the textures - ie image of earth
        //loads a bitmap, and returns it's location as an int
        public static int LoadTexture(Bitmap bitmap)
        {
            int texture;
            GL.GenTextures(1, out texture);
            GL.BindTexture(TextureTarget.Texture2D, texture);

            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, data.Width, data.Height, 0,
                OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);

            bitmap.UnlockBits(data);
            bitmap.Dispose();


            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Linear);


            return texture;
        }

        //variables

        //viewpoint
        Vector3 ViewPointV = new Vector3(-1f, -1f, 0f);

        public float ZoomMulti = 0.2f;

        //speed
        public int SimulationSpeed = 20;

        //size of grid
        int iSizeOfGrid = 1000;

        //grid
        int[,] arrAlive = new int[1000, 1000];

        public void Run()
        {
            using (var game = new GameWindow(700, 500, new GraphicsMode(32, 24, 0, 8)))
            {
                //seed array
                Random rand = new Random();
                for (int i = 200; i < iSizeOfGrid - 200; i++)
                {
                    for (int z = 200; z < iSizeOfGrid - 200; z++)
                    {
                        arrAlive[i, z] = rand.Next(3);
                    }
                }

                //Console.WriteLine (arrAlive.ToString ());


                //run at 60fps
                game.TargetRenderFrequency = 60;

                Matrix4 matrix = Matrix4.CreateTranslation(0, 0, 0);
                game.Load += (sender, e) =>
                {
                    // setup settings, load textures, sounds
                    game.VSync = VSyncMode.On;
                    game.Title = "LIFE";
                };

                game.Resize += (sender, e) => { GL.Viewport(0, 0, game.Width, game.Height); };

                //mouse click to add logic
                Vector2 MoCinitialvec = new Vector2(0f, 0f);
                Vector2 MoCdvec = new Vector2(0f, 0f);
                bool MoCdraw = false;
                Vector3 PrevViewpoint = new Vector3(0f, 0f, 0f);

                //enable textures
                GL.Enable(EnableCap.Texture2D);
                GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);


                //mouse click event
                game.Mouse.ButtonDown += (sender, e) =>
                {
                    MoCinitialvec = MousePosition(e.X, e.Y, game);
                    MoCdvec = MoCinitialvec;

                    //start adding an object
                    if (e.Button == MouseButton.Left)
                    {
                        MoCdraw = true;
                    }

                    //start moving viewpoint
                    if (e.Button == MouseButton.Right)
                    {
                        PrevViewpoint = ViewPointV;
                    }
                };

                game.Mouse.ButtonUp += (sender, e) =>
                {
                    //if left click - add object
                    if (e.Button == MouseButton.Left)
                    {
                        MoCdraw = false;
                    }
                };

                //scroll wheel - zoom in / out
                game.Mouse.WheelChanged += (sender, e) =>
                {
                    if (ZoomMulti - e.DeltaPrecise * 0.001f > 0)
                    {
                        ZoomMulti -= e.DeltaPrecise * 0.001f;
                    }
                };

                //moving mouse
                game.Mouse.Move += (sender, e) =>
                {
                    //for adding object
                    MoCdvec = MousePosition(e.X, e.Y, game);

                    if (e.X < 5 && e.Y < 5)
                    {
                        if (SimulationSpeed == 0)
                            SimulationSpeed = 20;
                        else
                            SimulationSpeed = 0;
                    }


                    if (e.Mouse.LeftButton == ButtonState.Pressed)
                    {
                        try
                        {
                            arrAlive[(int)MoCdvec.X, (int)MoCdvec.Y] = 1;
                        }
                        catch
                        {
                        }
                    }


                    //for moving viewpoint
                    if (e.Mouse.RightButton == ButtonState.Pressed)
                    {
                        ViewPointV = new Vector3((MoCdvec - MoCinitialvec).X / (game.Width * ZoomMulti),
                                         (MoCdvec - MoCinitialvec).Y / (game.Height * ZoomMulti), 0) + PrevViewpoint;
                    }
                };

                //keyboard input
                game.KeyPress += (sender, e) =>
                {
                    switch (e.KeyChar)
                    {
                        case 'f':
                            game.WindowState = WindowState.Fullscreen;
                            break;
                        case 'p':
                            if (SimulationSpeed == 0)
                                SimulationSpeed = 20;
                            else
                                SimulationSpeed = 0;
                            break;

                        case 'c':

                            //seed array
                            for (int i = 0; i < iSizeOfGrid; i++)
                            {
                                for (int z = 0; z < iSizeOfGrid; z++)
                                {
                                    arrAlive[i, z] = 0;
                                }
                            }
                            break;
                    }
                };

                //more keyboard input
                game.UpdateFrame += (sender, e) =>
                {
                    if (game.Keyboard[Key.Escape])
                    {
                        game.Exit();
                    }
                    if (game.Keyboard[Key.A])
                    {
                        ViewPointV.X += 0.01f;
                    }
                    if (game.Keyboard[Key.D])
                    {
                        ViewPointV.X -= 0.01f;
                    }
                    if (game.Keyboard[Key.W])
                    {
                        ViewPointV.Y -= 0.01f;
                    }
                    if (game.Keyboard[Key.S])
                    {
                        ViewPointV.Y += 0.01f;
                    }
                    if (game.Keyboard[Key.Z])
                    {
                        ZoomMulti += 0.0001f;
                    }
                    if (game.Keyboard[Key.X] && ZoomMulti > 0.001f)
                    {
                        ZoomMulti -= 0.0001f;
                    }
                };

                //for slowing down simulation
                int SimulationSlowDownStep = 0;

                game.RenderFrame += (sender, e) =>
                {
#if DEBUG
                    _frameRenderStopwatch.Restart();
#endif
                    
                    // Clear OpenGL window buffer.
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                    GL.MatrixMode(MatrixMode.Projection);

                    matrix = Matrix4.CreateTranslation(ViewPointV);

                    GL.LoadMatrix(ref matrix);
                    GL.Ortho(-game.Width * ZoomMulti, game.Width * ZoomMulti, -game.Height * ZoomMulti,
                        game.Height * ZoomMulti, 0.0, 4.0);

                    //speedup / slowdown
                    //slowdown?
                    if (SimulationSpeed < 20)
                    {
                        //Stop simulation when sim speed = 0
                        if (SimulationSpeed == 0)
                            SimulationSlowDownStep = 1;

                        SimulationSlowDownStep += SimulationSpeed;
                        if (SimulationSlowDownStep > 20)
                            SimulationSlowDownStep = 0;
                    }
                    else
                        SimulationSlowDownStep = 0;

#if DEBUG
                    _simulationStopwatch.Restart();
#endif
                    for (int zx = 20; (zx < SimulationSpeed || zx == 20) && SimulationSlowDownStep == 0; zx++)
                    {
                        int[,] arrNextTo = new int[iSizeOfGrid, iSizeOfGrid];


                        //calculate neighbours
                        for (int x = 0; x < iSizeOfGrid; x++)
                        {
                            for (int y = 0; y < iSizeOfGrid; y++)
                            {
                                if (arrAlive[x, y] == 1 && x > 1 && y > 1 && x < iSizeOfGrid - 1 && y < iSizeOfGrid - 1)
                                {
                                    arrNextTo[x - 1, y - 1]++;
                                    arrNextTo[x - 1, y]++;
                                    arrNextTo[x - 1, y + 1]++;
                                    arrNextTo[x, y + 1]++;
                                    arrNextTo[x, y - 1]++;
                                    arrNextTo[x + 1, y - 1]++;
                                    arrNextTo[x + 1, y]++;
                                    arrNextTo[x + 1, y + 1]++;
                                }
                            }
                        }

                        //kill / revive
                        for (int x = 0; x < iSizeOfGrid; x++)
                        {
                            for (int y = 0; y < iSizeOfGrid; y++)
                            {
                                if (arrNextTo[x, y] < 2 || arrNextTo[x, y] > 3)
                                {
                                    arrAlive[x, y] = 0;
                                }
                                if (arrNextTo[x, y] == 3)
                                {
                                    arrAlive[x, y] = 1;
                                }
                            }
                        }
                    }

#if DEBUG
                    _simulationStopwatch.Stop();
#endif

                    // Render graphics
                    // Now draw the objects to the gamewindow

                    GL.Color3(Color.Yellow);

                    GL.Begin(PrimitiveType.Quads);
                    //Draw Cells
                    for (int x = 0; x < iSizeOfGrid; x++)
                    {
                        //Console.WriteLine('n');
                        for (int y = 0; y < iSizeOfGrid; y++)
                        {
                            //	Console.Write(arrAlive[x,y]);
                            if (arrAlive[x, y] == 1)
                            {
                                GL.Vertex2(x, y);
                                GL.Vertex2(x, y + 1);
                                GL.Vertex2(x + 1, y + 1);
                                GL.Vertex2(x + 1, y);
                            }
                        }
                    }

                    GL.End();

                    DrawCircle(30, ViewPointV.X, ViewPointV.Y, 1);

                    //load onto screen
                    game.SwapBuffers();

#if DEBUG
                    _frameRenderStopwatch.Stop();
                    Console.WriteLine("Simulation: {0}ms", _simulationStopwatch.ElapsedMilliseconds);
                    Console.WriteLine("FrameRender: {0}ms", _frameRenderStopwatch.ElapsedMilliseconds);
#endif
                };

                //run the game loop
                game.Run();
            }
        }
    }
}