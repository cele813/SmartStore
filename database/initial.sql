CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "Categories" (
    "Id" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    CONSTRAINT "PK_Categories" PRIMARY KEY ("Id")
);

CREATE TABLE "SaleOrders" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SaleOrders" PRIMARY KEY ("Id")
);

CREATE TABLE "Suppliers" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Email" character varying(254),
    "Phone" character varying(30),
    CONSTRAINT "PK_Suppliers" PRIMARY KEY ("Id")
);

CREATE TABLE "Products" (
    "Id" uuid NOT NULL,
    "Sku" character varying(50) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Price" numeric(18,2) NOT NULL,
    "CategoryId" uuid NOT NULL,
    "SupplierId" uuid,
    "Stock" integer NOT NULL,
    CONSTRAINT "PK_Products" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_Product_Price" CHECK ("Price" >= 0),
    CONSTRAINT "CK_Product_Stock" CHECK ("Stock" >= 0),
    CONSTRAINT "FK_Products_Categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Products_Suppliers_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "SaleOrderItems" (
    "Id" uuid NOT NULL,
    "SaleOrderId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "Quantity" integer NOT NULL,
    "UnitPrice" numeric(18,2) NOT NULL,
    CONSTRAINT "PK_SaleOrderItems" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_Item_Price" CHECK ("UnitPrice" >= 0),
    CONSTRAINT "CK_Item_Quantity" CHECK ("Quantity" > 0),
    CONSTRAINT "FK_SaleOrderItems_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_SaleOrderItems_SaleOrders_SaleOrderId" FOREIGN KEY ("SaleOrderId") REFERENCES "SaleOrders" ("Id") ON DELETE CASCADE
);

CREATE TABLE "StockMovements" (
    "Id" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "Quantity" integer NOT NULL,
    "Reason" character varying(300) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_StockMovements" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_Movement_Quantity" CHECK ("Quantity" <> 0),
    CONSTRAINT "FK_StockMovements_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_Categories_Name" ON "Categories" ("Name");

CREATE INDEX "IX_Products_CategoryId" ON "Products" ("CategoryId");

CREATE UNIQUE INDEX "IX_Products_Sku" ON "Products" ("Sku");

CREATE INDEX "IX_Products_SupplierId" ON "Products" ("SupplierId");

CREATE INDEX "IX_SaleOrderItems_ProductId" ON "SaleOrderItems" ("ProductId");

CREATE INDEX "IX_SaleOrderItems_SaleOrderId" ON "SaleOrderItems" ("SaleOrderId");

CREATE INDEX "IX_StockMovements_ProductId" ON "StockMovements" ("ProductId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261023024113_InitialCreate', '10.0.12');

COMMIT;
