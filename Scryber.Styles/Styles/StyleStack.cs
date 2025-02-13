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
using System.Threading.Tasks;
using Scryber;

namespace Scryber.Styles
{
    public class StyleStack
    {
        

        private List<Style> _styles;

        public int Count
        {
            get { return this._styles.Count; }
        }

        public Style Current
        {
            get
            {
                int index = this.Count - 1;
                if (index < 0)
                    throw new InvalidOperationException("Cannot pop the style from an empty stack. The style stack has become unbalanced and has no items in it.");
                return _styles[index];
            }
        }

        public StyleStack(Style root)
        {
            this._styles = new List<Style>();
            this._styles.Add(root);
        }

        public void Push(Style style)
        {
            this._styles.Add(style);
        }

        public Style Pop()
        {
            int index = this.Count - 1;
            if(index < 0)
                throw new InvalidOperationException("Cannot pop the style from an empty stack. The style stack has become unbalanced and has no items in it.");
            Style last = this._styles[index];
            this._styles.RemoveAt(index);

            return last;
        }

        /// <summary>
        /// Creates a new style, populates all based upon the current styles
        /// </summary>
        /// <param name="Component"></param>
        /// <returns></returns>
        public Style GetFullStyle(IComponent Component)
        {
            Style style = BuildFullStyle(Component);
            return style;
        }

        private Style BuildFullStyle(IComponent Component)
        {
            Style style = new StyleFull();
            StyleVariableSet variables = null;

            int last = this._styles.Count - 1;
            if (last >= 0)
            {
                for (int i = 0; i < last; i++)
                {
                    //As these are styles from parents, then any inherited values should be replaced by
                    //explicit values on the last style
                    // so set the style priority to 0
                    var exist = this._styles[i];
                    exist.MergeInherited(style, replace:true, priority:0);

                    if(exist.HasVariables)
                    {
                        if (null == variables)
                            variables = new StyleVariableSet();
                        exist.Variables.MergeInto(variables);
                    }
                }

                //This will use to the priority of the value itself to be used

                this._styles[last].MergeInto(style);

                if(this._styles[last].HasVariables)
                {
                    if (null == variables)
                        variables = new StyleVariableSet();
                    this._styles[last].Variables.MergeInto(variables);
                }

                if (this._styles[last].HasStates)
                {
                    style.CopyStatesFrom(this._styles[last]);
                }
            }

            style.Variables = variables;
            style = style.Flatten();

            return style;
        }

        /// <summary>
        /// Create a new clone of the current stack with new references to the styles in the stack (inner styles are not cloned)
        /// </summary>
        /// <returns></returns>
        public StyleStack Clone()
        {
            StyleStack styles = this.MemberwiseClone() as StyleStack;
            styles._styles = new List<Style>();
            for (int i = 0; i < this._styles.Count; i++)
            {
                styles._styles.Add(this._styles[i]);
            }
            return styles;
        }
    }
}
