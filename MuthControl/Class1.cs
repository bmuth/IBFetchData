using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Muth.Framework
{

    public class MuthDataGridView : DataGridView
    {
        private LinkedList<ColSort> ColumnSortList = new LinkedList<ColSort> ();
        private bool bAdvancedSorting = false;
        private string sortorder;

        public string MultiSortOrder
        {
            get
            {
                return sortorder;
            }
        }

        new public object DataSource
        {
            get
            {
                return base.DataSource;
            }
            set
            {
                if (value.GetType () == typeof (BindingSource))
                {
                    bAdvancedSorting = ((BindingSource) value).SupportsAdvancedSorting;
                }
                base.DataSource = value;
                foreach (DataGridViewColumn col in Columns)
                {
                    col.SortMode = DataGridViewColumnSortMode.Programmatic;
                }
            }
        }

        protected override void OnColumnHeaderMouseClick (DataGridViewCellMouseEventArgs e)
        {
            if (bAdvancedSorting)
            {
                string str = Columns[e.ColumnIndex].DataPropertyName;

                ColSort cs = ColumnSortList.FirstOrDefault ();
                if (cs != null && str == cs.Name)
                {
                    cs.IfAscending = !cs.IfAscending;
                }
                else
                {
                    /* Make sure it isn't in the list
                       ------------------------------ */

                    LinkedListNode<ColSort> ll = ColumnSortList.First;
                    while (ll != null)
                    {
                        cs = (ColSort) ll.Value;
                        if (cs.Name == str)
                        {
                            ColumnSortList.Remove (ll);
                        }
                        ll = ll.Next;
                    }

                    ColumnSortList.AddFirst (new ColSort (str, true));
                    while (ColumnSortList.Count > 3)
                    {
                        ColumnSortList.RemoveLast ();
                    }
                }

                StringBuilder sb = new StringBuilder ();
                foreach (ColSort c in ColumnSortList)
                {
                    sb.Append (c.Name);
                    sb.Append (" ");
                    sb.Append (c.IfAscending ? "ASC, " : "DESC, ");
                }
                sortorder = sb.ToString ().TrimEnd (new char[] { ',', ' ' });
                ((BindingSource) DataSource).Sort = sortorder;

                // update glyph

                cs = ColumnSortList.First ();
                foreach (DataGridViewColumn col in Columns)
                {
                    if (cs.Name == col.DataPropertyName)
                    {
                        col.HeaderCell.SortGlyphDirection = cs.IfAscending ? SortOrder.Ascending : SortOrder.Descending;
                        break;
                    }
                }
            }
            else
            {
                base.OnColumnHeaderMouseClick (e);
            }
        }
    }
    internal class ColSort
    {
        public string Name;
        public bool IfAscending;

        public ColSort (string name, bool ifascending)
        {
            Name = name;
            IfAscending = ifascending;
        }
    }
}
