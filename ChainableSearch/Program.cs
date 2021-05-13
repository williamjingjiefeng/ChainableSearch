using System;
using ChainableSearch.Model;
using ChainableSearch.Services;

namespace ChainableSearch
{
    class Program
    {
        static void Main(string[] args)
        {
            var service = new ChainableService()
                .Search<Customer>(z => z.Id, z => z.Name)
                .Search<Friend>(z => z.Id, z => z.Name)
                .Search<Colleague>(z => z.Id, z => z.Name)
                .Search<Classmate>(z => z.Id, z => z.Name)
                .Insert<Contact>(z => z.Id, z => z.Name);

            var response = service.Action(1001);

            Console.WriteLine($"Found Id 1001's name is '{response.Data}'");

            response = service.Action(5005);

            Console.WriteLine($"Failed to find Id 5005's name, hence inserted into Contacts table with name '{response.Data}'");
        }
    }
}
