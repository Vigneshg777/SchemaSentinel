export interface SampleMigration {
  label: string;
  description: string;
  script: string;
}

/** Built-in sample scripts so the analyzer can be demonstrated immediately. */
export const SAMPLE_MIGRATIONS: SampleMigration[] = [
  {
    label: 'Destructive: DROP TABLE',
    description: 'Permanently removes a table and all of its data.',
    script: 'DROP TABLE Customers;',
  },
  {
    label: 'Column narrowing + NOT NULL',
    description: 'Reduces a column length and enforces NOT NULL.',
    script: 'ALTER TABLE Customers\nALTER COLUMN Email VARCHAR(50) NOT NULL;',
  },
  {
    label: 'Required column',
    description: 'Adds a NOT NULL column without a default to a populated table.',
    script: 'ALTER TABLE Orders\nADD PaymentReference VARCHAR(100) NOT NULL;',
  },
  {
    label: 'Dependency impact',
    description: 'Drops a table referenced by foreign keys.',
    script: 'DROP TABLE Customers;',
  },
  {
    label: 'Index operation',
    description: 'Creates an index — a potentially expensive operation.',
    script: 'CREATE INDEX IX_Orders_Status ON Orders (Status);',
  },
  {
    label: 'Safe: nullable column',
    description: 'Adds a nullable column — a low-risk change.',
    script: 'ALTER TABLE Customers\nADD LoyaltyCode VARCHAR(20) NULL;',
  },
];
