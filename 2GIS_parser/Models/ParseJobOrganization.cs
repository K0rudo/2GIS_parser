using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace _2GIS_parser.Models
{
    [PrimaryKey(nameof(ParseJobId), nameof(OrganizationId))]
    public class ParseJobOrganization
    {
        public int ParseJobId { get; set; }
        public int OrganizationId { get; set; }
    }
}