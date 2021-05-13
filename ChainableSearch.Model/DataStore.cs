using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ChainableSearch.Model
{
    public class DataStore
    {
        private static DataStore _instance;
        private static readonly object LockObject = new object();
        private readonly Dictionary<Type, IEnumerable<object>> _data = new Dictionary<Type, IEnumerable<object>>();

        public static DataStore Instance
        {
            get
            {
                if (_instance != null) return _instance;
                lock (LockObject)
                {
                    _instance ??= new DataStore();
                }

                return _instance;
            }
        }

        private DataStore()
        {
            _data[typeof(Customer)] = Customers;
            _data[typeof(Friend)] = Friends;
            _data[typeof(Colleague)] = Colleagues;
            _data[typeof(Classmate)] = Classmates;
            _data[typeof(Contact)] = Contacts;

            Contacts.CollectionChanged += Contacts_CollectionChanged;
        }

        private static void Contacts_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null && e.NewItems[0] is Contact contact)
            {
                contact.Name = contact.Id + " newly added";
            }
        }

        private static readonly List<Customer> Customers = new List<Customer>
        {
            new Customer
            {
                Name = "Adam",
                Id = 1001
            },

            new Customer
            {
                Name = "Becky",
                Id = 1002
            },

            new Customer
            {
                Name = "Sunny",
                Id = 1003
            }
        };

        private static readonly List<Friend> Friends = new List<Friend>
        {
            new Friend
            {
                Name = "Rachel",
                Id = 2001
            },

            new Friend
            {
                Name = "George",
                Id = 2002
            },

            new Friend
            {
                Name = "Stuart",
                Id = 2003
            }
        };

        private static readonly List<Colleague> Colleagues = new List<Colleague>
        {
            new Colleague
            {
                Name = "Raymond",
                Id = 3001
            },

            new Colleague
            {
                Name = "Simon",
                Id = 3002
            },

            new Colleague
            {
                Name = "Jessie",
                Id = 3003
            }
        };

        private static readonly List<Classmate> Classmates = new List<Classmate>
        {
            new Classmate
            {
                Name = "Chris",
                Id = 4001
            },

            new Classmate
            {
                Name = "Kevin",
                Id = 4002
            },

            new Classmate
            {
                Name = "Harry",
                Id = 4003
            }
        };

        private static readonly ObservableCollection<Contact> Contacts = new ObservableCollection<Contact>();

        public List<T> GetTable<T>()
        {
            return _data[typeof(T)] as List<T>;
        }

        public void AddTable<T>(T t)
        {
            (_data[typeof(T)] as ObservableCollection<T>)?.Add(t);
        }
    }
}
