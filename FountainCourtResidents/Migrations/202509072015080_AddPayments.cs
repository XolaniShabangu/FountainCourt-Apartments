namespace FountainCourtResidents.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPayments : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Payments",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ApplicationId = c.Int(nullable: false),
                        NationalId = c.String(maxLength: 13),
                        BuyerEmail = c.String(maxLength: 256),
                        Amount = c.Decimal(nullable: false, storeType: "money"),
                        Status = c.Int(nullable: false),
                        ProviderRef = c.String(maxLength: 64),
                        CreatedUtc = c.DateTime(nullable: false),
                        CompletedUtc = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.RentalApplications", t => t.ApplicationId, cascadeDelete: true)
                .Index(t => t.ApplicationId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Payments", "ApplicationId", "dbo.RentalApplications");
            DropIndex("dbo.Payments", new[] { "ApplicationId" });
            DropTable("dbo.Payments");
        }
    }
}
