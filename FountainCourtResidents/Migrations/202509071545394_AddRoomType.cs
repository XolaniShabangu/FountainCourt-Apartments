namespace FountainCourtResidents.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddRoomType : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.RoomTypes",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 64),
                        PricePerMonth = c.Decimal(nullable: false, precision: 18, scale: 2),
                        SquareMeters = c.Int(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        QuantityAvailable = c.Int(nullable: false),
                        TotalUnits = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.RoomTypes");
        }
    }
}
