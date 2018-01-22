from sqlalchemy import Column, Integer, String, Float, Boolean, ForeignKey
import sqlalchemy.types as types
from sqlalchemy.orm import relationship
from sqlalchemy.sql.expression import func
from sqlalchemy import or_, and_, desc
from marshmallow import Schema, fields, pre_dump
import time
from database import Base
from config import Cfg

cfg = Cfg()

class TransactionSchema(Schema):
    txid = fields.String()
    from_ = fields.String()
    to = fields.String()
    value = fields.String()
    block_num = fields.Integer()

    @pre_dump
    def hexify_txid(self, obj):
        obj.txid = "0x" + obj.txid.hex()
        return obj

class BigInt(types.TypeDecorator):
    """Convert ints to string in db and back to int on the way out."""

    impl = types.String

    def process_bind_param(self, value, dialect):
        return str(value)

    def process_result_value(self, value, dialect):
        return int(value)

class Transaction(Base):
    __tablename__ = 'transactions'
    id = Column(Integer, primary_key=True)
    account_id = Column(Integer, ForeignKey('accounts.id'))
    block_id = Column(Integer, ForeignKey('blocks.id'))
    txid = Column(String, nullable=False, unique=True)
    from_ = Column(String, nullable=False)
    to = Column(String, nullable=False)
    value = Column(BigInt)

    def __init__(self, account_id, block_id, txid, from_, to, value):
        self.account_id = account_id
        self.block_id = block_id
        self.txid = txid
        self.from_ = from_
        self.to = to
        self.value = value

    @classmethod
    def from_txid(cls, session, txid):
        return session.query(cls).filter(cls.txid == txid).first()

    def __repr__(self):
        return '<Transaction %r>' % (self.txid)

    def to_json(self, block_num=None):
        if block_num:
            self.block_num = block_num
        tx_schema = TransactionSchema()
        return tx_schema.dump(self).data

class AccountSchema(Schema):
    date = fields.Float()
    address = fields.String()
    active = fields.Boolean()

def simple_addr_list(accts):
    addresses = []
    for acct in accts:
        addresses.append(acct.address)
    return addresses

class Account(Base):
    __tablename__ = 'accounts'
    id = Column(Integer, primary_key=True)
    date = Column(Float, nullable=False, unique=False)
    address = Column(String, nullable=False, unique=True)
    active = Column(Boolean, nullable=False, default=True)
    transactions = relationship('Transaction')

    def __init__(self, address):
        self.date = time.time()
        self.address = address
        self.active = True

    @classmethod
    def from_address(cls, session, address):
        acct = session.query(cls).filter(cls.address == address).first()
        if acct:
            return acct
        return Account(address)

    @classmethod
    def count_txs(cls, session, address):
        acct = session.query(cls).filter(cls.address == address).first()
        if acct:
            return session.query(Transaction).filter(Transaction.account_id == acct.id).count()
        return 0

    @classmethod
    def has_txs(cls, session, addresses):
        result = []

        has_txs = cls.transactions.any()
        q = session.query(cls, has_txs).filter(cls.address.in_(addresses))
        for acct, has_txs in q.all():
            if has_txs:
                result.append(acct.address)
        return result

        #has_txs = cls.transactions.any()
        #q = session.query(cls, has_txs)
        #for acct, has_txs in q.all():
        #    if has_txs and acct.address in addresses:
        #        result.append(acct.address)
        #return result

    @classmethod
    def all_addresses(cls, session):
        return simple_addr_list(session.query(cls).all())

    @classmethod
    def all_active_addresses(cls, session):
        return simple_addr_list(session.query(cls).filter(cls.active == True).all())

    @classmethod
    def add_txs(cls, session, address, block_id, txs):
        acct = cls.from_address(session, address)
        for tx in txs:
            value = tx["value"]
            from_ = tx["from"]
            to = tx["to"]
            if isinstance(value, str):
                value = int(value, 16)
            tx_ = Transaction.from_txid(session, tx["hash"])
            if tx_:
                tx_.account_id = acct.id
                tx_.block_id = block_id
                tx_.from_ = from_
                tx_.to = to
                tx_.value = value
            else:
                tx_ = Transaction(acct.id, block_id, tx["hash"], from_, to, value)
            session.add(tx_)

    @classmethod
    def count(cls, session):
        return session.query(cls).count()

    @classmethod
    def count_active(cls, session):
        return session.query(cls).filter(cls.active == True).count()

    def to_json(self):
        account_schema = AccountSchema()
        return account_schema.dump(self).data

class Block(Base):
    __tablename__ = 'blocks'
    id = Column(Integer, primary_key=True)
    date = Column(Float, nullable=False, unique=False)
    num = Column(Integer, nullable=False)
    hash = Column(String, nullable=False, unique=True)
    reorged = Column(Boolean, nullable=False, default=False)
    transactions = relationship('Transaction')

    def __init__(self, block_num, block_hash):
        self.date = time.time()
        self.num = block_num
        self.hash = block_hash
        self.reorged = False

    def set_reorged(self, session):
        for tx in self.transactions:
            session.delete(tx)
        self.reorged = True
        session.add(self)

    @classmethod
    def last_block(cls, session):
        return session.query(cls).filter(cls.reorged == False).order_by(cls.id.desc()).first()

    @classmethod
    def from_number(cls, session, num):
        return session.query(cls).filter((cls.num == num) & (cls.reorged == False)).first()

    @classmethod
    def from_hash(cls, session, hash):
        return session.query(cls).filter(cls.hash == hash).first()

    @classmethod
    def tx_block_num(cls, session, tx_block_id):
        if tx_block_id:
            block = session.query(cls).filter(cls.id == tx_block_id).first()
            if block:
                return block.num 
        return -1

    @classmethod
    def tx_confirmations(cls, session, current_block_num, tx_block_id):
        block_num = cls.tx_block_num(session, tx_block_id)
        if block_num != -1:
                return current_block_num - block_num 
        return 0

    def __repr__(self):
        return '<Block %r %r>' % (self.num, self.hash)

class Setting(Base):
    __tablename__ = 'settings'
    id = Column(Integer, primary_key=True)
    key = Column(String, nullable=False, unique=True)
    value = Column(String, unique=False)

    def __init__(self, key, value):
        self.key = key
        self.value = value

    def __repr__(self):
        return '<Setting %r %r>' % (self.key, self.value)
