using System;
using System.Collections.Generic;
using System.ComponentModel;
using Muth.Framework;

namespace Muth.Framework
{
    public class SortableBindingListView<T> : BindingList<T>, IBindingListView
    {
        /***************************************************************
        * Constructors
        ***************************************************************/
        public SortableBindingListView ()
           : base (new List<T> ())
        {
            this.comparers = new Dictionary<Type, MyPropertyComparer<T>> ();
        }

        public SortableBindingListView (IEnumerable<T> enumeration)
            : base (new List<T> (enumeration))
        {
            this.comparers = new Dictionary<Type, MyPropertyComparer<T>> ();
        }

        /***************************************************************
         * multiple column sort support
         ***************************************************************/

        private ListSortDescriptionCollection _SortDescriptions;

        private List<MyPropertyComparer<T>> multicomparers;

        public ListSortDescriptionCollection SortDescriptions
        {
            get
            {
                return _SortDescriptions;
            }
        }

        public bool SupportsAdvancedSorting
        {
            get
            {
                return true;
            }
        }

        private int CompareValuesByProperties (T x, T y)
        {
            if (x == null)
                return (y == null) ? 0 : -1;
            else
            {
                if (y == null)
                    return 1;
                else
                {
                    foreach (MyPropertyComparer<T> comparer in multicomparers)
                    {
                        int retval = comparer.Compare (x, y);
                        if (retval != 0)
                        {
                            return retval;
                        }
                    }
                    return 0;
                }
            }

        }


        public void ApplySort (ListSortDescriptionCollection sorts)
        {
            List<T> items = this.Items as List<T>;

            // Apply and set the sort, if items to sort
            if (items != null)
            {
                _SortDescriptions = sorts;
                multicomparers = new List<MyPropertyComparer<T>> ();
                foreach (ListSortDescription sort in sorts)
                {
                    multicomparers.Add (new MyPropertyComparer<T> (sort.PropertyDescriptor, sort.SortDirection));
                }
                items.Sort (CompareValuesByProperties);
            }

            this.OnListChanged (new ListChangedEventArgs (ListChangedType.Reset, -1));
        }


        public string Filter
        {
            get
            {
                throw new NotImplementedException ();
            }
            set
            {
                throw new NotImplementedException ();
            }
        }

        public void RemoveFilter ()
        {
            throw new NotImplementedException ();
        }

        public bool SupportsFiltering
        {
            get
            {
                return false;
            }
        }

        /****************************************************************
        * support for sortable list stuff
        ****************************************************************/

        private readonly Dictionary<Type, MyPropertyComparer<T>> comparers;
        private bool isSorted;
        private ListSortDirection listSortDirection;
        private PropertyDescriptor propertyDescriptor;

        protected override bool SupportsSortingCore
        {
            get
            {
                return true;
            }
        }

        protected override bool IsSortedCore
        {
            get
            {
                return this.isSorted;
            }
        }

        protected override PropertyDescriptor SortPropertyCore
        {
            get
            {
                return this.propertyDescriptor;
            }
        }

        protected override ListSortDirection SortDirectionCore
        {
            get
            {
                return this.listSortDirection;
            }
        }

        protected override bool SupportsSearchingCore
        {
            get
            {
                return true;
            }
        }

        protected override void ApplySortCore (PropertyDescriptor property, ListSortDirection direction)
        {
            List<T> itemsList = (List<T>) this.Items;

            Type propertyType = property.PropertyType;
            MyPropertyComparer<T> comparer;
            if (!this.comparers.TryGetValue (propertyType, out comparer))
            {
                comparer = new MyPropertyComparer<T> (property, direction);
                this.comparers.Add (propertyType, comparer);
            }

            comparer.SetPropertyAndDirection (property, direction);
            itemsList.Sort (comparer);

            this.propertyDescriptor = property;
            this.listSortDirection = direction;
            this.isSorted = true;

            this.OnListChanged (new ListChangedEventArgs (ListChangedType.Reset, -1));
        }

        protected override void RemoveSortCore ()
        {
            this.isSorted = false;
            this.propertyDescriptor = base.SortPropertyCore;
            this.listSortDirection = base.SortDirectionCore;

            this.OnListChanged (new ListChangedEventArgs (ListChangedType.Reset, -1));
        }

        protected override int FindCore (PropertyDescriptor property, object key)
        {
            int count = this.Count;
            for (int i = 0; i < count; ++i)
            {
                T element = this[i];
                if (property.GetValue (element).Equals (key))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}