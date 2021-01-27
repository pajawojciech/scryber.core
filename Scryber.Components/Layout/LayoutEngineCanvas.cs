﻿/*  Copyright 2012 PerceiveIT Limited
 *  This file is part of the Scryber library.
 *
 *  You can redistribute Scryber and/or modify 
 *  it under the terms of the GNU Lesser General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 * 
 *  Scryber is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU Lesser General Public License for more details.
 * 
 *  You should have received a copy of the GNU Lesser General Public License
 *  along with Scryber source code in the COPYING.txt file.  If not, see <http://www.gnu.org/licenses/>.
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Scryber.Drawing;
using Scryber.Components;
using Scryber.Styles;
using Scryber.Resources;
using Scryber.Native;
using Scryber.Svg.Components;
using System.Runtime.CompilerServices;

namespace Scryber.Layout
{
    /// <summary>
    /// Implements the layout engine for a canvas component. This will set all the canvas' child
    /// components, to relatively positioned unless explictly set to Absolute
    /// </summary>
    public class LayoutEngineCanvas : LayoutEnginePanel
    {

        public PDFLayoutLine Line { get; set; }

        public LayoutEngineCanvas(ContainerComponent component, IPDFLayoutEngine parent)
            : base(component, parent)
        {
        }

        protected override void DoLayoutComponent()
        {
            PDFPositionOptions position = this.FullStyle.CreatePostionOptions();
            PDFLayoutXObject canvas = null;
            if (position.ViewPort.HasValue)
            {
                canvas = this.ApplyViewPort(position, position.ViewPort.Value);
            }

            base.DoLayoutComponent();

            if(null != canvas)
            {
                canvas.Close();
                
                this.CloseCurrentLine();

                canvas.OutPutName = (PDFName)this.Context.Document.GetIncrementID(PDFObjectTypes.CanvasXObject);
                var rsrc = new PDFCanvasResource(this.Component as Canvas, canvas, position.ViewPort.Value);
                var ratio = this.FullStyle.GetValue(SVGAspectRatio.AspectRatioStyleKey, SVGAspectRatio.Default);

                var size = new PDFSize(canvas.Width, canvas.Height);
                canvas.Matrix = CalculateMatrix(size, position.ViewPort.Value, ratio);
                canvas.ClipRect = new PDFRect(position.X.HasValue ? position.X.Value : PDFUnit.Zero,
                                              position.Y.HasValue ? position.Y.Value : PDFUnit.Zero,
                                              canvas.Width, canvas.Height);
                this.Context.DocumentLayout.CurrentPage.PageOwner.Register(rsrc);
                this.Context.Document.EnsureResource(rsrc.ResourceType, rsrc.ResourceKey, rsrc);
            }
        }


        private PDFTransformationMatrix CalculateMatrix(PDFSize available, PDFRect view, SVGAspectRatio ratio)
        {

            PDFTransformationMatrix matrix = PDFTransformationMatrix.Identity();
            if(ratio.Align == AspectRatioAlign.None)
            {
                SVGAspectRatio.ApplyMaxNonUniformScaling(matrix, available, view);

            }
            else if(ratio.Meet == AspectRatioMeet.Meet)
            {
                SVGAspectRatio.ApplyUniformScaling(matrix, available, view, ratio.Align);
            }
            
            return matrix;
        }


        protected virtual PDFLayoutXObject ApplyViewPort(PDFPositionOptions oldpos, PDFRect viewPort)
        {
            //Set the size to the viewport size
            var newpos = oldpos.Clone();
            newpos.X = viewPort.X;
            newpos.Y = viewPort.Y;

            //update to new widths
            newpos.Width = viewPort.Width;
            newpos.Height = viewPort.Height;

            //Set the style values to the viewport too. (and reset the cache)

            this.FullStyle.Size.Width = newpos.Width.Value;
            this.FullStyle.Size.Height = newpos.Height.Value;

            if (this.FullStyle is Scryber.Styles.StyleFull)
                (this.FullStyle as StyleFull).ClearFullRefs();

            PDFLayoutBlock containerBlock = this.DocumentLayout.CurrentPage.LastOpenBlock();
            PDFLayoutRegion containerRegion = containerBlock.CurrentRegion;
            if (containerRegion.HasOpenItem == false)
                containerRegion.BeginNewLine();
            //pos.Y = 200;
            PDFLayoutRegion container = containerBlock.BeginNewPositionedRegion(newpos, this.DocumentLayout.CurrentPage, this.Component, this.FullStyle, false);

            this.Line = containerRegion.CurrentItem as PDFLayoutLine;

            

            PDFLayoutXObject begin = this.Line.AddXObjectRun(this, this.Component, container, newpos, this.FullStyle);
            begin.SetOutputSize(oldpos.Width, oldpos.Height);

            
            //this.CurrentBlock.IsFormXObject = true;
            //this.CurrentBlock.XObjectViewPort = pos.ViewPort.Value;

            return begin;
        }

        #region protected override void DoLayoutAChild(IPDFComponent comp, Styles.PDFStyle full)

        /// <summary>
        /// Overrides the base implementation to set the explict position mode before
        /// continuing on as normal
        /// </summary>
        /// <param name="comp"></param>
        /// <param name="full"></param>
        protected override void DoLayoutAChild(IPDFComponent comp, Styles.Style full)
        {

            //For each child if there is not an explict Absolute setting then
            //we should treat them as relative
            Styles.PositionStyle pos = full.Position;
            PositionMode mode = pos.PositionMode;
            
            if (mode != PositionMode.Absolute)
            {
                pos.PositionMode = PositionMode.Relative;
            }

            base.DoLayoutAChild(comp, full);

        }

        #endregion

        protected virtual void AdjustContainerForTextBaseline(PDFPositionOptions pos, IPDFComponent comp, Style full)
        {
            var text = full.CreateTextOptions();
            
            if (text.DrawTextFromTop == false)
            {
                PDFUnit y;
                var font = full.CreateFont();
                if (pos.Y.HasValue)
                    y = pos.Y.Value;
                else
                    y = 0;

                var doc = this.Component.Document;
                var frsrc = doc.GetFontResource(font, true);
                var metrics = frsrc.Definition.GetFontMetrics(font.Size);

                //TODO: Register the font so that we can get the metrics. Or call later on and move
                // But for now it works (sort of).

                if (null != metrics)
                    y -= metrics.Ascent;
                else
                    y -= font.Size * 0.8;

                pos.Y = y;

                full.Position.Y = y;


                if (full is StyleFull)
                    (full as StyleFull).ClearFullRefs();
            }
        }

        protected override PDFLayoutRegion BeginNewRelativeRegionForChild(PDFPositionOptions pos, IPDFComponent comp, Style full)
        {
            this.AdjustContainerForTextBaseline(pos, comp, full);
            return base.BeginNewRelativeRegionForChild(pos, comp, full);
        }

        private void CloseCurrentLine()
        {

            if (!this.Line.IsClosed)
                this.Line.Region.CloseCurrentItem();
        }

        /// <summary>
        /// Wrapper resource class for the XObject layout block
        /// </summary>
        private class PDFCanvasResource : PDFResource
        {
            private PDFLayoutXObject _layout;
            private Canvas _component;

            public PDFCanvasResource(Canvas component, PDFLayoutXObject layout, PDFRect viewPort)
                : base(PDFObjectTypes.CanvasXObject)
            {
                this._layout = layout;
                this._component = component;
                this.Name = layout.OutPutName;
            }

            

            public override string ResourceType { get { return PDFResource.XObjectResourceType; } }

            public override string ResourceKey { get { return this._component.UniqueID; } }

            protected override PDFObjectRef DoRenderToPDF(PDFContextBase context, PDFWriter writer)
            {
                //The XObject should have been rendered as part of the page content.
                return _layout.RenderReference;
            }
        }
    }

   
}
